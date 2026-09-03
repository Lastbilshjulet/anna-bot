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
    private const int BytesPerSecond = 192000; // s16le, 48kHz, stereo: 2 bytes * 2 channels * 48000 samples/sec

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

    // Rough playback position of the current/most recent song, in seconds, used to
    // resume at (roughly) the same spot after a reconnect instead of from the start.
    private double _lastPlaybackPositionSeconds;

    // Set by Reconnect() right before it re-queues _lastPlayedSong; consumed once by
    // the playback loop the next time it starts streaming, then cleared.
    private double? _pendingResumeOffsetSeconds;
    
    private volatile bool _stopLoopRequested;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);

    public readonly SongQueue Queue = new(availableSongs);
    public ulong GuildId { get; } = guildId;

    // True only while a song is actively streaming (not paused, not idle).
    public bool IsPlaying { get; private set; }

    // True while playback is paused. Separate from IsPlaying so pausing doesn't fool
    // Enqueue()'s "should I auto-start the loop" check.
    public bool IsPaused { get; private set; }

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
                    IsPaused = false;

                    if (VoiceChannel.ConnectedUsers.Count <= 1)
                    {
                        // Honor the lifetime token so this doesn't keep the loop alive
                        // for up to 10s after a disconnect/dispose.
                        await Task.Delay(10000, _lifetimeCts.Token);
                        continue;
                    }

                    var song = Dequeue();
                    CurrentSong = song;

                    // Only overwrite _lastPlayedSong when we actually have a song - otherwise
                    // a queue running dry wipes out the very thing Reconnect() needs to replay.
                    if (song != null)
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

                    // Consumed at most once: only set (by Reconnect()) when this song is being
                    // resumed after a connection loss, otherwise it's null and we start at 0.
                    var resumeOffsetSeconds = _pendingResumeOffsetSeconds ?? 0;
                    _pendingResumeOffsetSeconds = null;
                    _lastPlaybackPositionSeconds = resumeOffsetSeconds;

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
                        await StreamAudioFromFile(songPath, resumeOffsetSeconds, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        logger.LogInformation("Skipped song: {Title}", song.Title);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error during audio streaming of song {Title}", song.Title);

                        // A single bad file shouldn't kill the loop, but a dead voice connection
                        // will fail on every subsequent song too - if we don't stop here, the loop
                        // burns through the entire queue in milliseconds, hits an empty queue, and
                        // tears the whole player down via DisconnectAsync()/DisposeAsync() before
                        // Reconnect() ever gets a chance to run. Stopping (without disposing) keeps
                        // the queue and _lastPlayedSong intact so Reconnect() can revive this same
                        // loop instead of finding a permanently-dead player.
                        if (_audioClient.ConnectionState != ConnectionState.Connected)
                        {
                            logger.LogWarning(
                                "Voice connection for guild {GuildId} appears lost; stopping playback loop until Reconnect() runs.",
                                GuildId);
                            break;
                        }
                    }
                    finally
                    {
                        lock (_lock)
                        {
                            _currentSongCts?.Dispose();
                            _currentSongCts = null;
                        }
                        CurrentSong = null;

                        // Moved here (was previously a standalone line after the try/catch/finally)
                        // so it's reset on every exit path, including `break` - a bare statement
                        // after the finally block is skipped when a break inside the catch fires.
                        IsPlaying = false;
                    }
                } while (!_stopLoopRequested);
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
        IsPaused = !IsPaused;

        if (!IsPaused)
            ResumeInternal();
        else
        {
            // Interlocked.CompareExchange instead of `_pauseTcs ??= ...`: the latter is a
            // read-then-conditionally-write, which is not atomic even on a volatile field.
            // This only actually allocates the new TCS if the field was still null.
            var candidate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.CompareExchange(ref _pauseTcs, candidate, null);
        }

        return IsPaused;
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
        // If the player was already fully torn down (e.g. someone ran "leave"), there's
        // nothing to revive - _lifetimeCts is cancelled/disposed and can never be reused.
        if (Volatile.Read(ref _disposed) == 1)
        {
            logger.LogWarning("Reconnect() called on an already-disposed player for guild {GuildId}; ignoring.", GuildId);
            return;
        }
        
        if (!await _reconnectLock.WaitAsync(0))
        {
            logger.LogInformation("Reconnect() already in progress for guild {GuildId}; ignoring duplicate call.", GuildId);
            return;
        }

        logger.LogInformation("Trying to reconnect");

        // The old connection may be silently dead (e.g. a voice-server migration) rather
        // than throwing, so the loop could be stuck forever inside WriteAsync/ReadAsync
        // without ever reaching its own exception handling. Force it to unblock now,
        // and mark this as a hard stop (not a Skip) so the loop exits instead of moving
        // on to the next song on the stale connection.
        _stopLoopRequested = true;
        lock (_lock)
            _currentSongCts?.Cancel();
        await DeleteMessageAsync();

        if (VoiceChannel == null)
            return;

        logger.LogInformation("Reconnecting to VoiceChannel");

        IAudioClient? newClient = null;
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                newClient = await VoiceChannel.ConnectAsync();
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Voice reconnect attempt {Attempt}/{Max} failed for guild {GuildId}", attempt, maxAttempts, GuildId);
                if (attempt == maxAttempts)
                {
                    logger.LogError("Giving up on voice reconnect for guild {GuildId} after {Max} attempts", GuildId, maxAttempts);
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
            }
        }

        _audioClient = newClient!;

        // Give Discord's voice gateway a moment to be ready to accept audio.
        await Task.Delay(1000);

        if (_lastPlayedSong != null)
        {
            logger.LogInformation(
                "Resuming {Title} at {Position:F1}s",
                _lastPlayedSong.Title, _lastPlaybackPositionSeconds);

            _pendingResumeOffsetSeconds = _lastPlaybackPositionSeconds;
            Queue.Enqueue(_lastPlayedSong);
            Queue.Cut();
        }

        // The old loop's task may still be unwinding (it observes the connection loss and
        // breaks out on its own) - wait for it so we don't race PlaySong() against a loop
        // that hasn't flipped _playbackLoopRunning back to false yet.
        Task previousLoop;
        lock (_lock) previousLoop = _playingTask;

        try
        {
            await previousLoop.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Timed out waiting for the previous playback loop to stop for guild {GuildId}", GuildId);
        }
        catch
        {
            // Any other exception from the old loop was already logged when it happened.
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

    private async Task StreamAudioFromFile(string filePath, double startOffsetSeconds = 0, CancellationToken cancellationToken = default)
    {
        using var ffmpeg = CreateFFmpegStream(filePath, startOffsetSeconds);
        await using var audioStream = _audioClient.CreatePCMStream(AudioApplication.Music);

        try
        {
            await CopyWithVolume(ffmpeg.StandardOutput.BaseStream, audioStream, cancellationToken, startOffsetSeconds);
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
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await audioStream.FlushAsync(cleanupCts.Token); } catch { /* Ignore fail */ }
            try { if (!ffmpeg.HasExited) ffmpeg.Kill(); } catch { /* Ignore fail */ }
            try { await ffmpeg.WaitForExitAsync(cleanupCts.Token); } catch { /* Ignore fail */ }
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

    private async Task CopyWithVolume(Stream source, Stream destination, CancellationToken cancellationToken, double baseOffsetSeconds)
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

        long totalBytesRead = 0;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);

            if (bytesRead <= 0)
                break;

            totalBytesRead += bytesRead;
            _lastPlaybackPositionSeconds = baseOffsetSeconds + (double)totalBytesRead / BytesPerSecond;

            await WaitIfPausedAsync(cancellationToken);

            var volume = Volume; // snapshot once per chunk to avoid tearing

            var offset = 0;
            // Output write cursor. Previously this reused the input index `i` once `offset`
            // was shifted by a carry byte, which meant the carry sample's second byte got
            // immediately clobbered by the first loop iteration, and every sample after it
            // was written one byte too early - corrupting audio (and reporting a writeLen
            // that included a byte that was never actually written). Tracking output
            // position separately from input position fixes both.
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
            RedirectStandardError = true // captured below instead of left open/unread
        };

        // ArgumentList handles per-OS quoting/escaping correctly, so a file path
        // containing spaces or quote characters can't break the command line.
        processStartInfo.ArgumentList.Add("-hide_banner");
        processStartInfo.ArgumentList.Add("-nostdin");
        processStartInfo.ArgumentList.Add("-loglevel");
        processStartInfo.ArgumentList.Add("warning");

        // -ss before -i does input seeking (fast, keyframe-based for most formats) so a
        // Reconnect() resume doesn't have to decode from the start of the file.
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
