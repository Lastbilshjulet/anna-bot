using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using anna_bot.Domain.Models.Configurations;
using anna_bot.InServices.Commands.Helpers;
using anna_bot.OutServices.UseCases;
using Discord.Audio;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace anna_bot.Domain.Models;

public class Player(
    ISongDbService songDbService,
    MusicConfiguration musicConfiguration,
    ulong guildId,
    IAudioClient audioClient,
    List<Song> availableSongs,
    Func<Task> onPlaybackEnded,
    ILogger<Player> logger)
{
    private readonly Lock _lock = new();
    private CancellationTokenSource? _currentSongCts;
    private RestUserMessage? _currentMessage;
    private readonly ManualResetEvent _pauseEvent = new(true);
    private IAudioClient _audioClient = audioClient;
    private Task _playingTask = Task.CompletedTask;
    private Song? _lastPlayedSong;

    public readonly SongQueue Queue = new(availableSongs);
    public ulong GuildId { get; } = guildId;
    public bool IsPlaying { get; private set; }
    public Song? CurrentSong { get; private set; }
    public float Volume { get; set; } = musicConfiguration.BaseVolume;
    public bool Repeat { get; private set; }

    public string DisplayVolume => $"{(int)(Volume * 100)}%";
    public SocketVoiceChannel? VoiceChannel { get; private set; }
    public SocketTextChannel? TextChannel { get; private set; }

    private void PlaySong()
    {
        if (TextChannel == null || VoiceChannel == null)
            return;
        
        _playingTask = Task.Run(async () => {
            do
            {
                Volume = musicConfiguration.BaseVolume;
                if (VoiceChannel.ConnectedUsers.Count <= 1)
                {
                    await Task.Delay(10000);
                    continue;
                }
                
                var song = Dequeue();
                CurrentSong = song;
                _lastPlayedSong = song;

                if (song == null)
                {
                    logger.LogInformation("No more songs found to play.");
                    await MessageHelper.EmbedSendMessageAsync(TextChannel!, "No more songs found to play.");
                    await DisconnectAsync();
                    return;
                }
                
                var songPath = song.GetFullPath(musicConfiguration.Path);
                if (!File.Exists(songPath))
                {
                    logger.LogError("Song file could not be fond on {Path}", songPath);
                    break;
                }
                
                try
                {
                    IsPlaying = true;
                    _pauseEvent.Set();

                    lock (_lock)
                    {
                        _currentSongCts?.Dispose();
                        _currentSongCts = new CancellationTokenSource();
                    }

                    if (TextChannel != null)
                    {
                        _currentMessage = await MessageHelper.EmbedSendMessageAsync(this, TextChannel!, song);
                    }
                    
                    songDbService.IncreasePlayAmount(song);
                    await StreamAudioFromFile(songPath, _currentSongCts.Token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Skipped song: {Title}", song.Title);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during audio streaming of song {Title}", song.Title);
                }
                finally
                {
                    lock (_lock)
                    {
                        _currentSongCts?.Dispose();
                        _currentSongCts = null;
                    }
                    CurrentSong = null;
                }
                IsPlaying = false;
            } while (true);
        });
    }

    private Song? Dequeue()
    {
        if (Repeat)
            Queue.QueueSameSongFirst();
        return Queue.Dequeue();
    }

    public void Enqueue(Song song, SocketTextChannel textChannel, SocketVoiceChannel voiceChannel)
    {
        Queue.Enqueue(song);
        TextChannel = textChannel;
        VoiceChannel = voiceChannel;
        if (Queue.Count == 1 && !IsPlaying)
        {
            PlaySong();
        }
    }

    public async Task Skip()
    {
        lock (_lock)
            _currentSongCts?.Cancel();
        
        if (!IsPlaying)
            _pauseEvent.Set();
        
        await DeleteMessageAsync();
    }

    public async Task PlayPreviousSong()
    {
        Queue.QueueFromHistoryFirst();
        await Skip();
    }

    public bool Pause()
    {
        IsPlaying = !IsPlaying;
        
        if (IsPlaying)
            _pauseEvent.Set();
        else
            _pauseEvent.Reset();
        
        return IsPlaying;
    }

    public void DecreaseVolume()
    {
        Volume -= 0.1f;
    }

    public void IncreaseVolume()
    {
        Volume += 0.1f;
    }

    public bool ToggleRepeat()
    {
        Repeat = !Repeat;
        return Repeat;
    }

    public async Task Reconnect()
    {
        logger.LogInformation("Trying to reconnect");
        await DeleteMessageAsync();

        if (VoiceChannel != null)
        {
            logger.LogInformation("Disposing of playing task");
            _playingTask.Dispose();

            await Task.Delay(2500);
            
            logger.LogInformation("Disconnecting from VoiceChannel");
            try
            {
                await VoiceChannel.DisconnectAsync();
            }
            catch
            {
                // ignored
            }

            await Task.Delay(2500);
            
            logger.LogInformation("Reconnecting to VoiceChannel");
            _audioClient = await VoiceChannel.ConnectAsync();

            await Task.Delay(2500);
            
            if (_lastPlayedSong != null)
            {
                logger.LogInformation("Playing LastPlayedSong again");
                Queue.Enqueue(_lastPlayedSong);
                Queue.Cut();
            }

            PlaySong();
        }
    }

    public async Task DisconnectAsync()
    {
        if (VoiceChannel != null)
        {
            await VoiceChannel.DisconnectAsync();
        }
        
        await DeleteMessageAsync();
        await onPlaybackEnded.Invoke();
    }

    private async Task DeleteMessageAsync()
    {
        try
        {
            if (_currentMessage != null)
                await _currentMessage.DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error deleting message, probably already deleted.");
        }
    }

    private async Task StreamAudioFromFile(string filePath, CancellationToken cancellationToken = default)
    {
        using var ffmpeg = CreateFFmpegStream(filePath);
        await using var audioStream = _audioClient.CreatePCMStream(AudioApplication.Mixed);

        try
        {
            await CopyWithVolume(ffmpeg.StandardOutput.BaseStream, audioStream, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Audio streaming was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during audio streaming");
            throw;
        }
        finally
        {
            try { await audioStream.FlushAsync(CancellationToken.None); } catch { /* Ignore fail */ }
            try { if (!ffmpeg.HasExited) ffmpeg.Kill(); } catch { /* Ignore fail */ }
            try { await ffmpeg.WaitForExitAsync(CancellationToken.None); } catch { /* Ignore fail */ }
        }
    }
    
    private async Task CopyWithVolume(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        // PCM s16le = 2 bytes per sample, 2 channels = 4 bytes per frame
        const int bufferSize = 7680; // 4 8000 Hz * 2 ch * 2 bytes * 20ms
        var buffer = new byte[bufferSize];
        var scaled = new byte[bufferSize];

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            _pauseEvent.WaitOne();
            
            var volume = Volume; // snapshot once per chunk to avoid tearing

            // Walk through each 16-bit little-endian sample and scale it
            for (var i = 0; i < bytesRead - 1; i += 2)
            {
                var sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                var scaled16 = (short)Math.Clamp(sample * volume, short.MinValue, short.MaxValue);
                scaled[i]     = (byte)(scaled16 & 0xFF);
                scaled[i + 1] = (byte)((scaled16 >> 8) & 0xFF);
            }

            await destination.WriteAsync(scaled.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private static Process CreateFFmpegStream(string filePath)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -i \"{filePath}\" -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = false
        };

        var process = Process.Start(processStartInfo);
        return process ?? throw new Exception("FFmpeg process start failed.");
    }
}
