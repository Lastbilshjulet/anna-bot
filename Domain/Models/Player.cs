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
    ILogger<Player> logger) : IAsyncDisposable
{
    private readonly Lock _lock = new();

    // Lifetime token: cancels everything (including the "waiting for listeners" delay)
    // when the player is torn down, instead of only being able to cancel the current song.
    private readonly CancellationTokenSource _lifetimeCts = new();

    private CancellationTokenSource? _currentSongCts;
    private RestUserMessage? _currentMessage;

    // Async-friendly pause gate. When null, playback is not paused.
    // When set, the streaming loop awaits this task instead of blocking a thread.
    private volatile TaskCompletionSource<bool>? _pauseTcs;

    private IAudioClient _audioClient = audioClient;
    private Task _playingTask = Task.CompletedTask;
    private Song? _lastPlayedSong;
    private bool _playbackLoopRunning;
    private int _disposed; // 0 = false, 1 = true; guarded with Interlocked, see DisposeAsync

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
        {
            lock (_lock) _playbackLoopRunning = false;
            return;
        }

        _playingTask = Task.Run(async () =>
        {
            try
            {
                do
                {
                    Volume = musicConfiguration.BaseVolume;
                    if (VoiceChannel.ConnectedUsers.Count <= 1)
                    {
                        // Honor the lifetime token so this doesn't keep the loop alive
                        // for up to 10s after a disconnect/dispose.
                        await Task.Delay(10000, _lifetimeCts.Token);
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
                        logger.LogError("Song file could not be found at {Path}", songPath);
                        break;
                    }

                    try
                    {
                        IsPlaying = true;
                        ResumeInternal();

                        CancellationTokenSource cts;
                        lock (_lock)
                        {
                            _currentSongCts?.Dispose();
                            // Linked to the lifetime token, so a full teardown cancels
                            // whatever song is currently streaming too.
                            _currentSongCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
                            cts = _currentSongCts;
                        }

                        if (TextChannel != null)
                        {
                            _currentMessage = await MessageHelper.EmbedSendMessageAsync(this, TextChannel!, song);
                        }

                        songDbService.IncreasePlayAmount(song);
                        await StreamAudioFromFile(songPath, cts.Token);
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
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Playback loop cancelled for guild {GuildId}", GuildId);
            }
            finally
            {
                lock (_lock) _playbackLoopRunning = false;
            }
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

        // Guard against a double-start: IsPlaying only flips to true once the loop
        // actually dequeues a song, so two Enqueue calls arriving close together
        // (e.g., queueing several tracks quickly) could both see IsPlaying == false
        // and both call PlaySong(), producing two competing consumers of the queue.
        lock (_lock)
        {
            if (Queue.Count == 1 && !IsPlaying && !_playbackLoopRunning)
            {
                _playbackLoopRunning = true;
                PlaySong();
            }
        }
    }

    public async Task Skip()
    {
        lock (_lock)
            _currentSongCts?.Cancel();

        // No longer need to force-resume here: WaitIfPausedAsync observes the
        // cancellation token directly, so cancelling works even while paused.
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
            ResumeInternal();
        else
        {
            // Interlocked.CompareExchange instead of `_pauseTcs ??= ...`: the latter is a
            // read-then-conditionally-write, which is not atomic even on a volatile field.
            // This only actually allocates the new TCS if the field was still null.
            var candidate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.CompareExchange(ref _pauseTcs, candidate, null);
        }

        return IsPlaying;
    }

    private void ResumeInternal()
    {
        var tcs = Interlocked.Exchange(ref _pauseTcs, null);
        tcs?.TrySetResult(true);
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
            // Don't Dispose() a Task that may still be running - just let it complete
            // on its own (it will observe the disconnect/cancellation and exit).
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

            lock (_lock)
            {
                if (!_playbackLoopRunning)
                {
                    _playbackLoopRunning = true;
                    PlaySong();
                }
            }
        }
    }

    public async Task DisconnectAsync()
    {
        if (VoiceChannel != null)
        {
            await VoiceChannel.DisconnectAsync();
        }

        await DeleteMessageAsync();

        // Note: DisconnectAsync can be called both externally (e.g., a "leave" command)
        // and internally from inside the playback loop itself (queue ran out). DisposeAsync
        // doesn't await the loop's task, so calling it from either place is safe.
        await DisposeAsync();

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
        await using var audioStream = _audioClient.CreatePCMStream(AudioApplication.Music);

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

    private Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        var tcs = _pauseTcs;
        // tcs.Task.WaitAsync(token) means Skip()/cancellation works instantly even
        // while paused, unlike the old ManualResetEvent.WaitOne() which ignored the
        // token entirely and could only be unblocked by manually calling Set().
        return tcs?.Task.WaitAsync(cancellationToken) ?? Task.CompletedTask;
    }

    // If a single read or write takes longer than this, it's eating into the 20ms
    // real-time budget for this chunk and will be audible as a stutter. Logged so you
    // can tell, next time it lags, whether the stall is on the read side (disk/ffmpeg)
    // or the writing side (Discord/network).
    private const int SlowChunkThresholdMs = 30;

    private async Task CopyWithVolume(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        // s16le, 48kHz, stereo = 2 bytes/sample * 2 channels * 48000 samples/sec
        // = 192,000 bytes/sec -> 3840 bytes per 20ms frame (matches Discord's Opus frame size).
        const int bufferSize = 3840;
        var buffer = new byte[bufferSize];
        var scaled = new byte[bufferSize];

        // Carries a single leftover byte across reads if a chunk arrives with an odd
        // length, so we never scale/write a stale or half-written sample.
        var haveCarry = false;
        byte carryByte = 0;

        var sw = new Stopwatch();

        while (true)
        {
            sw.Restart();
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            sw.Stop();
            if (sw.ElapsedMilliseconds > SlowChunkThresholdMs)
                logger.LogWarning("Slow ffmpeg/disk read: {Ms}ms for {Bytes} bytes (song {Title})",
                    sw.ElapsedMilliseconds, bytesRead, CurrentSong?.Title);

            if (bytesRead <= 0)
                break;

            await WaitIfPausedAsync(cancellationToken);

            var volume = Volume; // snapshot once per chunk to avoid tearing

            var offset = 0;
            var writeLen = 0;

            if (haveCarry)
            {
                var sample = (short)(carryByte | (buffer[0] << 8));
                var scaled16 = (short)Math.Clamp(sample * volume, short.MinValue, short.MaxValue);
                scaled[0] = (byte)(scaled16 & 0xFF);
                scaled[1] = (byte)((scaled16 >> 8) & 0xFF);
                offset = 1;
                writeLen = 2;
                haveCarry = false;
            }

            var pairEnd = bytesRead - ((bytesRead - offset) % 2 == 0 ? 0 : 1);
            for (var i = offset; i < pairEnd - 1; i += 2)
            {
                var sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                var scaled16 = (short)Math.Clamp(sample * volume, short.MinValue, short.MaxValue);
                scaled[i] = (byte)(scaled16 & 0xFF);
                scaled[i + 1] = (byte)((scaled16 >> 8) & 0xFF);
                writeLen += 2;
            }

            if (pairEnd < bytesRead)
            {
                carryByte = buffer[bytesRead - 1];
                haveCarry = true;
            }

            if (writeLen <= 0) 
                continue;
            
            sw.Restart();
            await destination.WriteAsync(scaled.AsMemory(0, writeLen), cancellationToken);
            sw.Stop();
            if (sw.ElapsedMilliseconds > SlowChunkThresholdMs)
                logger.LogWarning("Slow write to Discord PCM stream: {Ms}ms for {Bytes} bytes (song {Title})",
                    sw.ElapsedMilliseconds, writeLen, CurrentSong?.Title);
        }
    }

    private Process CreateFFmpegStream(string filePath)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true // captured below instead of left open/unread
        };

        // ArgumentList handles per-OS quoting/escaping correctly, so a file path
        // containing spaces or quote characters can't break the command line.
        processStartInfo.ArgumentList.Add("-hide_banner");
        processStartInfo.ArgumentList.Add("-nostdin");
        processStartInfo.ArgumentList.Add("-loglevel");
        processStartInfo.ArgumentList.Add("warning");
        processStartInfo.ArgumentList.Add("-i");
        processStartInfo.ArgumentList.Add(filePath);
        processStartInfo.ArgumentList.Add("-ac");
        processStartInfo.ArgumentList.Add("2");
        processStartInfo.ArgumentList.Add("-f");
        processStartInfo.ArgumentList.Add("s16le");
        processStartInfo.ArgumentList.Add("-ar");
        processStartInfo.ArgumentList.Add("48000");
        processStartInfo.ArgumentList.Add("pipe:1");

        var process = Process.Start(processStartInfo)
            ?? throw new Exception("FFmpeg process start failed.");

        // Drain stderr asynchronously via the event-based reader. If this weren't
        // read at all while RedirectStandardError = true, the OS pipe buffer could
        // fill up and block ffmpeg's writes to it, stalling the whole stream.
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                logger.LogWarning("ffmpeg[{File}]: {Line}", Path.GetFileName(filePath), e.Data);
        };
        process.BeginErrorReadLine();

        return process;
    }

    public ValueTask DisposeAsync()
    {
        // Idempotent: DisconnectAsync (which calls this) can run more than once in edge
        // cases - e.g., a "leave" command racing with the queue naturally running out.
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return ValueTask.CompletedTask;

        // Cancel() (not CancelAsync) runs synchronously: by the time this line returns,
        // every reader of this token - the ReadAsync/WriteAsync calls in CopyWithVolume,
        // and the pause-gate's WaitAsync - already sees IsCancellationRequested == true
        // and will unwind on it's very next await. Audio stops here, not after cleanup.
        _lifetimeCts.Cancel();

        // We deliberately do NOT await _playingTask here - the caller wants playback to
        // stop immediately, not to wait for ffmpeg to actually exit and the stream to be
        // flushed/killed. That cleanup still happens (it's in the loop's own final
        // blocks), just in the background. We only need to keep _lifetimeCts alive until
        // then, since a pending token registration on an already-disposed source would
        // throw ObjectDisposedException.
        _ = _playingTask.ContinueWith(t =>
        {
            if (t.IsFaulted)
                logger.LogError(t.Exception, "Error while stopping playback during dispose");

            _lifetimeCts.Dispose();
        }, TaskContinuationOptions.ExecuteSynchronously);
        
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }
}
