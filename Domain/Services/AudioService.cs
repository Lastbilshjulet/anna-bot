using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using anna_bot.Domain.Models;
using anna_bot.Domain.Models.Configurations;
using anna_bot.OutServices.UseCases;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace anna_bot.Domain.Services;

public class AudioService(
    IYoutubeService youtubeService, 
    ISpotifyService spotifyService, 
    ISongDbService songDbService,
    IDiscordDmService discordDmService,
    PlayerState playerState, 
    IOptions<MusicConfiguration> musicConfig, 
    ILogger<AudioService> logger) : IAudioService
{
    public async Task<Song?> SearchAndFetchAsync(string query, SocketGuildUser? guildUser)
    {
        Song? song = null;
        List<Song> playlistSongs = [];
        if (youtubeService.ValidatePlaylistUri(query))
        {
            logger.LogInformation("Trying to fetch youtube playlist for {Url}", query);
            var songs = await youtubeService.GetPlaylistDetails(query);
            if (songs.Count == 0)
                return null;
            
            song = songs.First();
            playlistSongs = songs;
        }
        else if (spotifyService.ValidatePlaylistUri(query))
        {
            logger.LogInformation("Trying to fetch spotify playlist details for {Url}", query);
            var songs = await spotifyService.GetPlaylistAlbumDetails(query);
            if (songs.Count == 0)
                return null;
            
            song = songs.First();
            playlistSongs = songs;
        }

        if (song == null)
        {
            if (youtubeService.ValidateVideoUri(query))
            {
                logger.LogInformation("Trying to fetch youtube details for {Url}", query);
                song = await youtubeService.GetVideoDetails(query);
            }
            else if (spotifyService.ValidateTrackUri(query))
            {
                logger.LogInformation("Trying to fetch spotify details for {Url}", query);
                song = await spotifyService.GetTrackDetails(query);
            }
        }
        
        if (song == null || string.IsNullOrEmpty(song.YoutubeId))
        {
            logger.LogInformation("Trying to fetch youtube video for {Query}", query);
            song = await youtubeService.Search(query, song);
        }

        if (song == null)
            return null;

        song = await ProcessSong(guildUser, song);

        if (playlistSongs.Count > 1)
        {
            _ = ProcessPlaylistSongsInBackground(guildUser, playlistSongs.Skip(1).ToList());
        }

        return song;
    }

    public async Task<Song?> SearchSpotifyAndUpdateAsync(Song song)
    {
        var spotifySong = await SearchSpotifyAsync(song);
        
        return songDbService.UpdateSpotifyId(song.YoutubeId, spotifySong?.SpotifyId ?? "");
    }

    private async Task<Song?> SearchSpotifyAsync(Song song)
    {
        var spotifySong = await spotifyService.SearchTrackAsync($"{song.Title} {song.Artist}") ?? await spotifyService.SearchTrackAsync($"{song.Title}");
        if (spotifySong == null)
            return null;

        if (song.Title.Contains(spotifySong.Title))
        {
            await spotifyService.AddSongToPlaylistAsync(spotifySong);
            return spotifySong;
        }
        
        logger.LogWarning("Song {SongTitle} ({YoutubeId}) might not match song {SpotifySongTitle} ({SpotifyId}). Will try to search for only title", song.Title, song.YoutubeId, spotifySong.Title, spotifySong.SpotifyId);

        spotifySong = await spotifyService.SearchTrackAsync($"{song.Title}");
        if (spotifySong == null)
            return null;

        if (song.Title.Contains(spotifySong.Title))
        {
            await spotifyService.AddSongToPlaylistAsync(spotifySong);
            return spotifySong;
        }

        await discordDmService.SendSpotifySongMismatchNotification(song, spotifySong);
        logger.LogWarning("Song {SongTitle} ({YoutubeId}) might not match song {SpotifySongTitle} ({SpotifyId}). Will not save id", song.Title, song.YoutubeId, spotifySong.Title, spotifySong.SpotifyId);
        return null;
    }

    private async Task<Song?> ProcessSong(SocketGuildUser? guildUser, Song song)
    {
        try
        {
            var alreadyExistingSong = playerState.GetAllAvailableSongs().FirstOrDefault(x => x.YoutubeId == song.YoutubeId);
            if (alreadyExistingSong != null)
            {
                if (alreadyExistingSong.SpotifyId == null)
                    await SearchSpotifyAndUpdateAsync(alreadyExistingSong);
                return alreadyExistingSong;
            }

            var path = await youtubeService.DownloadSong(song);
            
            if (path == null)
                return null;

            var spotifySong = await SearchSpotifyAsync(song);
            song.SpotifyId ??= spotifySong?.SpotifyId ?? "";
            
            song.Path = path;
            song.Extension = musicConfig.Value.Extension;
            song.RequestedBy = guildUser?.Username ?? "UnknownUser";
            song.RequestedByUserId = guildUser?.Id ?? 0;

            logger.LogInformation("Inserting {SongTitle} ({YoutubeId}) into database", song.Title, song.YoutubeId);
            song = songDbService.InsertSong(song);

            return song;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing song {SongTitle} ({YoutubeId})", song.Title, song.YoutubeId);
            return null;
        }
    }

    private async Task ProcessPlaylistSongsInBackground(SocketGuildUser? guildUser, List<Song> songList)
    {
        Player? player = null;
        foreach (var playlistSong in songList)
        {
            Song? song = null;
            if (string.IsNullOrEmpty(playlistSong.YoutubeId))
            {
                logger.LogInformation("Trying to fetch youtube video for {SpotifyId} ({Title} - {Artist})", playlistSong.SpotifyId, playlistSong.Title, playlistSong.Artist);
                song = await youtubeService.Search("", playlistSong);
                if (song == null)
                    continue;
            }
            
            song = await ProcessSong(guildUser, song ?? playlistSong);

            player ??= await WaitForPlayerAsync(guildUser!.Guild.Id, timeoutSeconds: 30);

            if (song == null || player == null)
                continue;
            
            playerState.AddSong(guildUser!.Guild.Id, song, player.TextChannel!, player.VoiceChannel!);
            logger.LogInformation("Added song {SongTitle} from playlist to player in guild {GuildId}", song.Title, guildUser.Guild.Id);
        }
    }

    private async Task<Player?> WaitForPlayerAsync(ulong guildId, int timeoutSeconds)
    {
        await Task.Delay(1000);
        var stopwatch = Stopwatch.StartNew();
        var timeoutSpan = TimeSpan.FromSeconds(timeoutSeconds);

        while (stopwatch.Elapsed < timeoutSpan)
        {
            var player = playerState.GetExistingPlayer(guildId);
            if (player != null)
                return player;

            logger.LogInformation("Waiting for player in guild {GuildId}", guildId);
            await Task.Delay(100);
        }

        return null;
    }
}
