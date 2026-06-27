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
    
    public Player(ISongDbService songDbService, MusicConfiguration musicConfiguration, ulong guildId, IAudioClient audioClient, List<Song> availableSongs)
    {
        _songDbService = songDbService;
        _musicConfiguration = musicConfiguration;
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
        using var ffmpeg = CreateFFmpegStream(filePath, _musicConfiguration.BaseVolume);
        await using var audioStream = _audioClient.CreatePCMStream(AudioApplication.Mixed);

        try
        {
            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(audioStream, cancellationToken);
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
            try
            {
                await audioStream.FlushAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            { /* Ignore flush cancellation */ }

            try
            {
                if (!ffmpeg.HasExited)
                    ffmpeg.Kill();

                await ffmpeg.WaitForExitAsync(cancellationToken);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }

    private static Process CreateFFmpegStream(string filePath, float volume)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -i \"{filePath}\" -filter:a \"volume={volume.ToString(CultureInfo.InvariantCulture)}\" -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = false
        };

        var process = Process.Start(processStartInfo);
        return process ?? throw new Exception("FFmpeg process start failed.");
    }
}
