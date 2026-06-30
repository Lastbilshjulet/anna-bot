using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using anna_bot.Domain.Models;
using anna_bot.OutServices.UseCases;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;

namespace anna_bot.OutServices;

public partial class SpotifyService(
    SpotifyClient spotifyClient,
    ILogger<SpotifyService> logger) : ISpotifyService
{
    [GeneratedRegex(@"^(https ?:\/\/)?(www\.)?(open\.| play\.)?spotify\.com\/.+$", RegexOptions.IgnoreCase, "sv-SE")]
    private static partial Regex TrackRegex();
    [GeneratedRegex(@"^(https ?:\/\/)?(www\.)?(open\.| play\.)?spotify\.com\/ playlist\/.+$", RegexOptions.IgnoreCase, "sv-SE")]
    private static partial Regex PlaylistRegex();
    
    public bool ValidateTrackUri(string uri)
    {
        return TrackRegex().IsMatch(uri);
    }

    public bool ValidatePlaylistUri(string uri)
    {
        return PlaylistRegex().IsMatch(uri);
    }

    public async Task<Song?> GetTrackDetails(string uri)
    {
        logger.LogInformation("Getting spotify track details for uri: {Uri}", uri);
        var trackId = ExtractTrackId(uri);
        if (string.IsNullOrEmpty(trackId))
        {
            logger.LogWarning("Could not extract track id from uri: {Uri}", uri);
            return null;
        }

        try
        {
            logger.LogInformation("Getting spotify track details for track id: {TrackId}", trackId);
            var track = await spotifyClient.Tracks.Get(trackId);

            return new Song
            {
                SpotifyId = trackId,
                Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
                Title = track.Name,
                Source = track.ExternalUrls["spotify"],
                Duration = TimeSpan.FromMilliseconds(track.DurationMs)
            };
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

    private static string? ExtractTrackId(string url)
    {
        var patterns = new[]
        {
            "spotify:track:([a-zA-Z0-9]+)",
            @"open\.spotify\.com/track/([a-zA-Z0-9]+)",
            @"spotify\.com/track/([a-zA-Z0-9]+)"
        };

        return (from pattern 
                in patterns 
                select Regex.Match(url, pattern) 
                into match 
                where match.Success 
                select match.Groups[1].Value).FirstOrDefault();
    }

    public async Task<List<Song>?> GetPlaylistDetails(string uri)
    {
        List<Song>? songs = null;
        return await Task.FromResult(songs);
    }
}
