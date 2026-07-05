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
using YoutubeExplode;
using YoutubeExplode.Converter;

namespace anna_bot.OutServices;

public partial class YoutubeService(
    YoutubeClient youtubeClient,
    IOptions<MusicConfiguration> musicConfig, 
    ILogger<YoutubeService> logger) : IYoutubeService
{
    [GeneratedRegex(@"^(https?:\/\/)?(www\.)?(youtube\.com\/watch\?v=|youtu\.be\/)([a-zA-Z0-9_-]{11})(\S*)?$", RegexOptions.IgnoreCase, "sv-SE")]
    private static partial Regex VideoRegex();
    
    [GeneratedRegex(@"^(https?:\/\/)?(www\.)?(youtube\.com\/(playlist|watch)\?(.*\&)?list=)([a-zA-Z0-9_-]+)(\S*)?$", RegexOptions.IgnoreCase, "sv-SE")]
    private static partial Regex PlaylistRegex();
    
    public bool ValidateVideoUri(string uri)
    {
        return VideoRegex().IsMatch(uri);
    }

    public bool ValidatePlaylistUri(string uri)
    {
        return PlaylistRegex().IsMatch(uri);
    }

    public async Task<Song?> Search(string query, Song? song)
    {
        if (song != null && string.IsNullOrEmpty(song.YoutubeId))
            query = $"{song.Title} {song.Artist}";
        var searchResult = youtubeClient.Search.GetVideosAsync(query);
        var video = await searchResult.FirstOrDefaultAsync();

        if (video == null)
        {
            logger.LogWarning("No video found from query: {Query}", query);
            return null;
        }

        var thumbnailUrl = string.Empty;
        if (video.Thumbnails.Count > 0)
            thumbnailUrl = video.Thumbnails[0].Url;
        
        return new Song
        {
            YoutubeId = video.Id.Value,
            SpotifyId = song?.SpotifyId,
            Title = song?.Title ?? video.Title,
            Artist = song?.Artist ?? video.Author.ChannelTitle,
            Duration =  video.Duration ?? song?.Duration ?? TimeSpan.Zero,
            Thumbnail = thumbnailUrl,
            Source = song?.Source ?? video.Url
        };
    }

    public async Task<Song?> GetVideoDetails(string uri)
    {
        var video = await youtubeClient.Videos.GetAsync(uri);

        var thumbnailUrl = string.Empty;
        if (video.Thumbnails.Count > 0)
            thumbnailUrl = video.Thumbnails[0].Url;
        
        return new Song
        {
            YoutubeId = video.Id.Value,
            Title = video.Title,
            Artist = video.Author.ChannelTitle,
            Duration = video.Duration ?? TimeSpan.Zero,
            Thumbnail = thumbnailUrl,
            Source = video.Url
        };
    }

    public async Task<List<Song>> GetPlaylistDetails(string uri)
    {
        var result = youtubeClient.Playlists.GetVideosAsync(uri);
        
        return await result
            .Select(video => new Song
            {
                YoutubeId = video.Id.Value,
                Title = video.Title,
                Artist = video.Author.ChannelTitle,
                Duration = video.Duration ?? TimeSpan.Zero,
                Thumbnail = video.Thumbnails.Count > 0 ? video.Thumbnails[0].Url : string.Empty,
                Source = video.Url
            })
            .ToListAsync();
    }

    public async Task<string?> DownloadSong(Song song)
    {
        try
        {
            if (string.IsNullOrEmpty(song.YoutubeId))
                return null;

            var cleanTitle = song.CleanTitle();
            var fullPath = song.GetFullPath(musicConfig.Value.Path, musicConfig.Value.Extension);
            await youtubeClient.Videos.DownloadAsync(song.GetYouTubeUrl(), fullPath, o => o
                .SetContainer(musicConfig.Value.Extension.Replace(".", "")));

            return cleanTitle;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading song {SongTitle} ({YoutubeId})", song.Title, song.YoutubeId);
            return null;
        }
    }
}
