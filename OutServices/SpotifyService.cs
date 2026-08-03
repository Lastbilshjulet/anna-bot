using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using anna_bot.Domain.Models;
using anna_bot.Domain.Models.Configurations;
using anna_bot.OutServices.UseCases;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace anna_bot.OutServices;

public partial class SpotifyService(
    SpotifyClient spotifyClient,
    IOptions<SpotifyConfiguration> spotifyConfiguration,
    ILogger<SpotifyService> logger) : ISpotifyService
{
    [GeneratedRegex(@"^(https ?:\/\/)?(www\.)?(open\.|play\.)?spotify\.com\/.+$", RegexOptions.IgnoreCase, "sv-SE")]
    private static partial Regex TrackRegex();
    [GeneratedRegex(@"^(https ?:\/\/)?(www\.)?(open\.|play\.)?spotify\.com\/playlist\/.+$", RegexOptions.IgnoreCase, "sv-SE")]
    private static partial Regex PlaylistRegex();
    [GeneratedRegex(@"^(https ?:\/\/)?(www\.)?(open\.|play\.)?spotify\.com\/album\/.+$", RegexOptions.IgnoreCase, "sv-SE")]
    private static partial Regex AlbumRegex();
    
    public bool ValidateTrackUri(string uri)
    {
        return TrackRegex().IsMatch(uri);
    }

    public bool ValidatePlaylistUri(string uri)
    {
        return PlaylistRegex().IsMatch(uri) || AlbumRegex().IsMatch(uri);
    }

    public async Task<Song?> GetTrackDetails(string uri)
    {
        logger.LogInformation("Getting spotify track details for uri: {Uri}", uri);
        var trackId = ExtractTrackId("track", uri);
        if (string.IsNullOrEmpty(trackId))
        {
            logger.LogWarning("Could not extract track id from uri: {Uri}", uri);
            return null;
        }

        try
        {
            logger.LogInformation("Getting spotify track details for track id: {TrackId}", trackId);
            var track = await spotifyClient.Tracks.Get(trackId);

            return FullTrackToSong(track);
        }
        catch (APIUnauthorizedException)
        {
            logger.LogError("Spotify auth failed.");
        }
        catch (APITooManyRequestsException)
        {
            logger.LogError("Sent too many requests to spotify.");
        }
        catch (APIException exc)
        {
            logger.LogError("Failed request to spotify. Message: {ExcMessage}", exc.Message);
        }
        
        return null;
    }

    private static Song FullTrackToSong(FullTrack track)
    {
        return new Song
        {
            SpotifyId = track.Id,
            Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
            Title = track.Name,
            Source = track.ExternalUrls["spotify"],
            Duration = TimeSpan.FromMilliseconds(track.DurationMs)
        };
    }

    public async Task<Song?> SearchTrackAsync(string query)
    {
        var searchRequest = new SearchRequest(SearchRequest.Types.Track, query);
        var searchResponse = await spotifyClient.Search.Item(searchRequest);

        var track = searchResponse.Tracks.Items?.FirstOrDefault();

        if (track == null)
            return null;

        var mappedTrack = FullTrackToSong(track);
        logger.LogInformation("Searched spotify for {Query}: Found {TrackTitle} - {TrackArtist}", query, mappedTrack.Title, mappedTrack.Artist);
        
        return mappedTrack;
    }

    private static string? ExtractTrackId(string type, string url)
    {
        var patterns = new[]
        {
            $"spotify:{type}:([a-zA-Z0-9]+)",
            $@"open\.spotify\.com/{type}/([a-zA-Z0-9]+)",
            $@"spotify\.com/{type}/([a-zA-Z0-9]+)"
        };

        return (from pattern 
                in patterns 
                select Regex.Match(url, pattern) 
                into match 
                where match.Success 
                select match.Groups[1].Value).FirstOrDefault();
    }

    public async Task<List<Song>> GetPlaylistAlbumDetails(string uri)
    {
        var isAlbum = AlbumRegex().IsMatch(uri);
        logger.LogInformation("Getting spotify playlist/album details for uri: {Uri}", uri);
        var id = ExtractTrackId(isAlbum ? "album" : "playlist", uri);
        if (string.IsNullOrEmpty(id))
        {
            logger.LogWarning("Could not extract playlist/album id from uri: {Uri}", uri);
            return [];
        }

        try
        {
            logger.LogInformation("Getting spotify playlist/album details for id: {TrackId}", id);

            List<Song> songs;
            if (isAlbum)
            {
                var request = new AlbumTracksRequest { Limit = 50 };
                var album = await spotifyClient.Albums.GetTracks(id, request);
                if (album.Items == null)
                    return [];
                
                songs = album.Items
                    .Where(x => x.Type == ItemType.Track)
                    .Select(x => new Song
                    {
                        SpotifyId = x.Id,
                        Artist = string.Join(", ", x.Artists.Select(a => a.Name)),
                        Title = x.Name,
                        Source = x.ExternalUrls["spotify"],
                        Duration = TimeSpan.FromMilliseconds(x.DurationMs)
                    }).ToList();
            }
            else
            {
                var request = new PlaylistGetItemsRequest { Limit = 100 };
                var playlist = await spotifyClient.Playlists.GetPlaylistItems(id, request);
                if (playlist.Items == null)
                    return [];
                
                songs = playlist.Items
                    .Select(x => x.Track switch
                    {
                        FullTrack { Type: ItemType.Track } fullTrack => new Song
                        {
                            SpotifyId = fullTrack.Id,
                            Artist = string.Join(", ", fullTrack.Artists.Select(a => a.Name)),
                            Title = fullTrack.Name,
                            Source = fullTrack.ExternalUrls["spotify"],
                            Duration = TimeSpan.FromMilliseconds(fullTrack.DurationMs)
                        },
                        _ => null
                    })
                    .Where(song => song != null)
                    .ToList()!;
            }

            return songs;
        }
        catch (APIUnauthorizedException)
        {
            logger.LogError("Spotify auth failed.");
        }
        catch (APITooManyRequestsException)
        {
            logger.LogError("Sent too many requests to spotify.");
        }
        catch (APIException exc)
        {
            logger.LogError("Failed request to spotify. Message: {ExcMessage}", exc.Message);
        }
        
        return [];
    }

    public async Task AddSongToPlaylistAsync(Song song)
    {
        await AddSongToPlaylistAsync([CreateSpotifyTrackString(song.SpotifyId!)]);
        
        logger.LogInformation("Added song {SongTitle} ({SpotifyId}) to spotify playlist.", song.Title, song.SpotifyId);
    }

    private async Task AddSongToPlaylistAsync(List<string> spotifyTrackStrings)
    {
        try
        {
            for (var i = 0; i < spotifyTrackStrings.Count; i += 100)
            {
                var listPart = spotifyTrackStrings.Skip(i).Take(100).ToList();
                var request = new PlaylistAddItemsRequest(listPart);
                await spotifyClient.Playlists.AddPlaylistItems(spotifyConfiguration.Value.PlaylistId, request);
        
                logger.LogInformation("Added {SongCount} songs to spotify playlist.", listPart.Count);
            }
        }
        catch (APIUnauthorizedException)
        {
            logger.LogError("Spotify auth failed.");
        }
        catch (APITooManyRequestsException)
        {
            logger.LogError("Sent too many requests to spotify.");
        }
        catch (APIException exc)
        {
            logger.LogError("Failed request to spotify. Message: {ExcMessage}", exc.Message);
        }
    }

    public async Task SyncPlaylist(HashSet<string> spotifyIds)
    {
        var playlistPages = await spotifyClient.Playlists.GetPlaylistItems(spotifyConfiguration.Value.PlaylistId);
        List<string> idsInPlaylist = [];
        
        await foreach (var song in spotifyClient.Paginate(playlistPages))
        {
            if (song.Track is FullTrack track)
            {
                idsInPlaylist.Add(track.Id);
            }
        }
        
        var itemsToAdd = spotifyIds.Except(idsInPlaylist).ToList();
        
        if (itemsToAdd.Count > 0)
        {
            await AddSongToPlaylistAsync(itemsToAdd.Select(CreateSpotifyTrackString).ToList());
        }
    }

    private static string CreateSpotifyTrackString(string spotifyTrackId)
    {
        return $"spotify:track:{spotifyTrackId}";
    }
}
