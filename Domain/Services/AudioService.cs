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
        // TODO: Save spotifyId on each song
        Song? song;
        if (youtubeService.ValidateVideoUri(query))
        {
            logger.LogInformation("Trying to fetch youtube details for {URL}", query);
            song = await youtubeService.GetVideoDetails(query);
        }
        else if (spotifyService.ValidateTrackUri(query))
        {
            logger.LogInformation("Trying to fetch spotify details for {URL}", query);
            song = await spotifyService.GetTrackDetails(query);
        }
        else
        {
            logger.LogInformation("Trying to fetch youtube video for {URL}", query);
            song = await youtubeService.Search(query);
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

        logger.LogInformation("Inserting {SongTitle} ({YoutubeId}) into database", song.Title, song.YoutubeId);
        song = songDbService.InsertSong(song);

        return song;
    }
}
