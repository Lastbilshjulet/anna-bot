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
    PlayerHolder playerHolder, 
    IOptions<MusicConfiguration> musicConfig, 
    ILogger<AudioService> logger) : IAudioService
{
    public async Task<Song?> SearchAndFetch(string query, SocketGuildUser? guildUser)
    {
        Song? song = null;
        if (youtubeService.ValidateVideoUri(query))
        {
            //TODO: Check cache of youtube ids before searching
            logger.LogInformation("Trying to fetch youtube details for {Url}", query);
            song = await youtubeService.GetVideoDetails(query);
        }
        else if (spotifyService.ValidateTrackUri(query))
        {
            //TODO: Add and check cache of spotify ids before searching
            logger.LogInformation("Trying to fetch spotify details for {Url}", query);
            song = await spotifyService.GetTrackDetails(query);
        }
        
        if (song == null || string.IsNullOrEmpty(song.YoutubeId))
        {
            logger.LogInformation("Trying to fetch youtube video for {Query}", query);
            song = await youtubeService.Search(query, song);
        }

        if (song == null)
            return null;
        
        // TODO: Try to find spotifyId from YouTube video

        var alreadyExistingSong = playerHolder.GetAllAvailableSongs().FirstOrDefault(x => x.YoutubeId == song.YoutubeId);
        if (alreadyExistingSong != null)
            return alreadyExistingSong;
        
        var path = await youtubeService.DownloadSong(song);
        if (path == null)
            return null;
        
        song.Path = path;
        song.Extension = musicConfig.Value.Extension;
        song.RequestedBy = guildUser?.Username ?? "UnknownUser";
        song.RequestedByUserId = guildUser?.Id ?? 0;

        logger.LogInformation("Inserting {SongTitle} ({YoutubeId}) into database", song.Title, song.YoutubeId);
        song = songDbService.InsertSong(song);

        return song;
    }
}
