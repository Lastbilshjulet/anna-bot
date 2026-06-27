using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using anna_bot.Domain.Models.Configurations;
using anna_bot.OutServices.UseCases;
using Discord.Audio;
using Microsoft.Extensions.Logging;

namespace anna_bot.Domain.Models;

public class Player
{
    private readonly ISongDbService _songDbService;
    private readonly MusicConfiguration _musicConfiguration;
    private readonly SongQueue _queue;
    private readonly Lock _lock = new();
    private readonly IAudioClient _audioClient;
    private readonly ILogger _logger;
    private CancellationTokenSource? _currentSongCts;

    public ulong GuildId { get; }
    public bool IsPlaying { get; private set; }
    public Song? CurrentSong { get; private set; }
    public float Volume { get; set; }
    
    public Player(ISongDbService songDbService, MusicConfiguration musicConfiguration, ulong guildId, IAudioClient audioClient, List<Song> availableSongs)
    {
        _songDbService = songDbService;
        _musicConfiguration = musicConfiguration;
        Volume = musicConfiguration.BaseVolume;
        GuildId = guildId;
        _audioClient = audioClient;
        _queue = new SongQueue(availableSongs);

        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<Player>();
    }

    private void PlaySong()
    {
        _ = Task.Run(async () => {
            do
            {
                Volume = _musicConfiguration.BaseVolume;
                var song = Dequeue();
                CurrentSong = song;
                
                if (song != null)
                {
                    var songPath = song.GetFullPath(_musicConfiguration.Path);
                    if (!File.Exists(songPath))
                    {
                        _logger.LogError("Song file could not be fond on {Path}", songPath);
                        break;
                    }
                 
                    try
                    {
                        IsPlaying = true;

                        lock (_lock)
                        {
                            _currentSongCts?.Dispose();
                            _currentSongCts = new CancellationTokenSource();
                        }

                        _songDbService.IncreasePlayAmount(song);
                        await StreamAudioFromFile(songPath, _currentSongCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Skipped song: {Title}", song.Title);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during audio streaming of song {Title}", song.Title);
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
                }
                IsPlaying = false;
            } while (_queue.Count >= 1);
        });
    }

    public void Enqueue(Song song)
    {
        _queue.Enqueue(song);
        if (_queue.Count == 1 && !IsPlaying)
        {
            PlaySong();
        }
    }

    private Song? Dequeue()
    {
        return _queue.Count == 0 ? null : _queue.Dequeue();
    }

    public void Skip()
    {
        lock (_lock)
        {
            _currentSongCts?.Cancel();
        }
    }

    public void AddUnplayed(Song song)
    {
        _queue.AddUnplayed(song);
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
            _logger.LogWarning(ex, "Audio streaming was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during audio streaming");
            throw;
        }
        finally
        {
            try { await audioStream.FlushAsync(CancellationToken.None); } catch { }
            try { if (!ffmpeg.HasExited) ffmpeg.Kill(); } catch { }
            try { await ffmpeg.WaitForExitAsync(CancellationToken.None); } catch { }
        }
    }
    
    private async Task CopyWithVolume(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        // PCM s16le = 2 bytes per sample, 2 channels = 4 bytes per frame
        const int bufferSize = 3840; // 48000 Hz * 2 ch * 2 bytes * 20ms
        var buffer = new byte[bufferSize];
        var scaled = new byte[bufferSize];

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
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
