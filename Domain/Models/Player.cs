using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using anna_bot.Domain.Models.Configurations;
using anna_bot.InServices.Commands.Helpers;
using anna_bot.OutServices.UseCases;
using Discord;
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
    ILogger<Player> logger) : IAsyncDisposable
{
    private IAudioClient _audioClient = audioClient;
    private CancellationTokenSource _lifetimeCts = new();

    private CancellationTokenSource? _currentSongCts;
    private RestUserMessage? _currentMessage;
    private Task _playingTask = Task.CompletedTask;

    private Song? _lastPlayedSong;
    private bool _isSeeking;
    
    public ulong GuildId => guildId;
    public SocketVoiceChannel? VoiceChannel { get; private set; }
    public SocketTextChannel? TextChannel { get; private set; }
    
    public readonly SongQueue Queue = new(availableSongs);

    public bool IsPlaying { get; private set; }
    private bool IsPaused { get; set; }

    public Song? CurrentSong { get; private set; }
    public double CurrentTime { get; private set; }

    public float Volume { get; set; } = musicConfiguration.BaseVolume;
    public bool Repeat { get; private set; }

    public string DisplayVolume => $"{(int)(Volume * 100)}%";

    private void PlaySong()
    {
        if (TextChannel == null || VoiceChannel == null)
        {
            return;
        }

        _playingTask = Task.Run(SongPlayingTask);
    }

    private async Task SongPlayingTask()
    {
        try
        {
            var retries = 0;

            do
            {
                if (_audioClient.ConnectionState != ConnectionState.Connected)
                {
                    retries++;
                    await Task.Delay(1000);
                    if (retries > 5)
                    {
                        logger.LogWarning("Audio client is not connected, disposing");
                        break;
                    }
                    
                    logger.LogWarning("Audio client is not connected, retrying");
                    continue;
                }

                retries = 5;
                
                if (VoiceChannel!.ConnectedUsers.Count <= 1)
                {
                    await Task.Delay(10000, _lifetimeCts.Token);
                    continue;
                }

                IsPaused = false;
                Volume = musicConfiguration.BaseVolume;
                CurrentSong = Dequeue();
                if (CurrentSong == null)
                {
                    logger.LogInformation("No more songs found to play");
                    await MessageHelper.EmbedSendMessageAsync(TextChannel!, "No more songs found to play.");
                    await DisconnectAsync();
                    break;
                }

                var songPath = CurrentSong.GetFullPath(musicConfiguration.Path);
                if (!File.Exists(songPath))
                {
                    logger.LogError("Song file could not be found at {Path}", songPath);
                    await MessageHelper.EmbedSendMessageAsync(TextChannel!, "Song could not be found.");
                    break;
                }

                _lastPlayedSong = CurrentSong;
                if (!_isSeeking)
                    CurrentTime = 0;

                try
                {
                    _isSeeking = false;
                    IsPlaying = true;
                    _currentSongCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

                    _currentMessage = await MessageHelper.EmbedSendMessageAsync(this, TextChannel!, CurrentSong);

                    songDbService.IncreasePlayAmount(CurrentSong);
                    await StreamAudioFromFile(songPath, CurrentTime, _currentSongCts.Token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Skipped song: {Title}", CurrentSong.Title);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Song was unexpectedly cancelled through exception");
                }
                finally
                {
                    await DeleteMessageAsync();
                    IsPlaying = false;
                    _currentSongCts?.Dispose();
                    _currentSongCts = null;
                    CurrentSong = null;
                }
            } while (!_lifetimeCts.IsCancellationRequested);

            if (retries > 5)
            {
                await DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error playing song");
        }
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
        
        logger.LogInformation("{Title} was enqueued in guild {GuildId}", song.Title, GuildId);

        if (IsPlaying || Queue.Count != 1)
            return;
        
        PlaySong();
    }

    public async Task Skip()
    {
        if (_currentSongCts != null)
            await _currentSongCts.CancelAsync();
        
        await DeleteMessageAsync();
    }

    public async Task PlayPreviousSong()
    {
        Queue.QueueFromHistoryFirst();
        await Skip();
    }

    public bool Pause()
    {
        IsPaused = !IsPaused;
        return IsPaused;
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

    public async Task Seek(int seconds)
    {
        await _lifetimeCts.CancelAsync();
        await _playingTask;
        
        _lifetimeCts = new CancellationTokenSource();

        if (_lastPlayedSong != null)
        {
            logger.LogInformation("Seeking to {Seconds} seconds in {CurrentSongTitle}", seconds, _lastPlayedSong.Title);
            CurrentTime = seconds;
            _isSeeking = true;
            Queue.Enqueue(_lastPlayedSong);
            Queue.Cut();
            PlaySong();
        }
    }

    public async Task DisconnectAsync()
    {
        await _lifetimeCts.CancelAsync();
        await _playingTask;
        
        await DeleteMessageAsync();
        
        logger.LogInformation("Invoking to remove player ({GuildId}) from playerState", GuildId);
        await onPlaybackEnded.Invoke();
        
        if (VoiceChannel != null)
        {
            await VoiceChannel.DisconnectAsync();
            logger.LogInformation("Disconnecting from voice channel {VoiceChannelName} ({VoiceChannelId})", VoiceChannel.Name, VoiceChannel.Id);
        }
        else
        {
            logger.LogWarning("No voice channel to disconnect");
        }
        
        await DisposeAsync();
    }

    private async Task DeleteMessageAsync()
    {
        logger.LogInformation("Deleting message");
        try
        {
            if (_currentMessage != null)
                await _currentMessage.DeleteAsync();
            else
                logger.LogWarning("No message to delete");
        }
        catch (Exception)
        {
            logger.LogWarning("Error deleting message, probably already deleted");
        }
    }

    public async Task UpdateSongMessageAsync()
    {
        logger.LogInformation("Updating message");
        try
        {
            if (_currentMessage != null && CurrentSong != null)
                await MessageHelper.EmbedUpdateMessageAsync(this, _currentMessage);
            else
                logger.LogWarning("No message to update, no song playing");
        }
        catch (Exception)
        {
            logger.LogWarning("Error updating message, probably deleted");
        }
    }

    public async Task Reconnect()
    {
        logger.LogInformation("Reconnecting player ({GuildId})", GuildId);

        if (VoiceChannel == null)
        {
            logger.LogWarning("No voice channel to reconnect to");
            return;
        }

        try
        {
            _audioClient = await VoiceChannel.ConnectAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting to voice channel during reconnect");
            return;
        }
        
        await _lifetimeCts.CancelAsync();
        await _playingTask;
        
        _lifetimeCts = new CancellationTokenSource();

        if (_lastPlayedSong != null)
        {
            _isSeeking = true;
            Queue.Enqueue(_lastPlayedSong);
            Queue.Cut();
            PlaySong();
        }
    }
    
    private async Task StreamAudioFromFile(string filePath, double startOffsetSeconds = 0, CancellationToken cancellationToken = default)
    {
        using var ffmpeg = CreateFFmpegStream(filePath, startOffsetSeconds);
        await using var audioStream = _audioClient.CreatePCMStream(AudioApplication.Music);

        var irreparableDamage = false;
        try
        {
            await CopyWithVolume(ffmpeg.StandardOutput.BaseStream, audioStream, startOffsetSeconds, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Audio streaming was cancelled, most likely from a skip");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during audio streaming");
            throw;
        }
        finally
        {
            logger.LogInformation("Stream over, cleaning up audio stream");
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await audioStream.FlushAsync(cleanupCts.Token);
            }
            catch
            {
                logger.LogWarning("Failed to flush audio stream");
                irreparableDamage = true;
            }

            try
            {
                if (!ffmpeg.HasExited)
                    ffmpeg.Kill();
            }
            catch
            {
                logger.LogWarning("Failed to kill ffmpeg process");
                irreparableDamage = true;
            }

            try
            {
                await ffmpeg.WaitForExitAsync(cleanupCts.Token);
            }
            catch
            {
                logger.LogWarning("Failed to wait for ffmpeg process to exit");
                irreparableDamage = true;
            }
        }

        if (irreparableDamage)
        {
            logger.LogError("Failed to cleanup audio stream, disconnecting");
            await DisconnectAsync();
        }
    }
    
    private async Task WaitIfPausedAsync(CancellationToken cancellationToken = default)
    {
        while (IsPaused)
            await Task.Delay(100, cancellationToken);
    }
    
    private async Task CopyWithVolume(Stream source, Stream destination, double baseOffsetSeconds, CancellationToken cancellationToken = default)
    {
        const int bufferSize = 3840;
        var buffer = new byte[bufferSize];
        var scaled = new byte[bufferSize];

        var haveCarry = false;
        byte carryByte = 0;

        long totalBytesRead = 0;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);

            if (bytesRead <= 0)
                break;

            totalBytesRead += bytesRead;
            CurrentTime = baseOffsetSeconds + (double)totalBytesRead / 192000;

            await WaitIfPausedAsync(cancellationToken);

            var volume = Volume;

            var offset = 0;
            var writeIdx = 0;

            if (haveCarry)
            {
                var sample = (short)(carryByte | (buffer[0] << 8));
                var scaled16 = (short)Math.Clamp(sample * volume, short.MinValue, short.MaxValue);
                scaled[0] = (byte)(scaled16 & 0xFF);
                scaled[1] = (byte)((scaled16 >> 8) & 0xFF);
                offset = 1;
                writeIdx = 2;
                haveCarry = false;
            }

            var pairEnd = bytesRead - ((bytesRead - offset) % 2 == 0 ? 0 : 1);
            for (var i = offset; i < pairEnd - 1; i += 2)
            {
                var sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                var scaled16 = (short)Math.Clamp(sample * volume, short.MinValue, short.MaxValue);
                scaled[writeIdx] = (byte)(scaled16 & 0xFF);
                scaled[writeIdx + 1] = (byte)((scaled16 >> 8) & 0xFF);
                writeIdx += 2;
            }

            if (pairEnd < bytesRead)
            {
                carryByte = buffer[bytesRead - 1];
                haveCarry = true;
            }

            if (writeIdx <= 0)
                continue;

            await destination.WriteAsync(scaled.AsMemory(0, writeIdx), cancellationToken);
        }
    }

    private Process CreateFFmpegStream(string filePath, double startOffsetSeconds = 0)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        processStartInfo.ArgumentList.Add("-hide_banner");
        processStartInfo.ArgumentList.Add("-nostdin");
        processStartInfo.ArgumentList.Add("-loglevel");
        processStartInfo.ArgumentList.Add("warning");

        if (startOffsetSeconds > 0.01)
        {
            processStartInfo.ArgumentList.Add("-ss");
            processStartInfo.ArgumentList.Add(startOffsetSeconds.ToString("0.00", CultureInfo.InvariantCulture));
        }

        processStartInfo.ArgumentList.Add("-i");
        processStartInfo.ArgumentList.Add(filePath);
        processStartInfo.ArgumentList.Add("-ac");
        processStartInfo.ArgumentList.Add("2");
        processStartInfo.ArgumentList.Add("-f");
        processStartInfo.ArgumentList.Add("s16le");
        processStartInfo.ArgumentList.Add("-ar");
        processStartInfo.ArgumentList.Add("48000");
        processStartInfo.ArgumentList.Add("pipe:1");

        var process = Process.Start(processStartInfo) ?? throw new Exception("FFmpeg process start failed.");

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                logger.LogWarning("ffmpeg[{File}]: {Line}", Path.GetFileName(filePath), e.Data);
        };
        process.BeginErrorReadLine();
        
        logger.LogInformation("Created ffmpeg process for {File} (pid {Pid})", Path.GetFileName(filePath), process.Id);

        return process;
    }

    public async ValueTask DisposeAsync()
    {
        await CastAndDispose(_lifetimeCts);
        await CastAndDispose(_currentSongCts);
        await CastAndDispose(_playingTask);
        await CastAndDispose(_audioClient);
        
        GC.SuppressFinalize(this);

        return;

        static async ValueTask CastAndDispose(IDisposable? resource)
        {
            switch (resource)
            {
                case null:
                    return;
                case IAsyncDisposable resourceAsyncDisposable:
                    await resourceAsyncDisposable.DisposeAsync();
                    break;
                default:
                    resource.Dispose();
                    break;
            }
        }
    }
}
