using System;
using System.Linq;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.Domain.Services;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class SearchSpotifyAndAddToPlaylist(
    PlayerState playerState,
    IAudioService audioService,
    ILogger<SearchSpotifyAndAddToPlaylist> logger, 
    ICommandLogger<SearchSpotifyAndAddToPlaylist> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("searchspotifyandaddtoplaylist", "Searches spotify and adds to playlist.")]
    public async Task SearchSpotifyAndAddToPlaylistAsync()
    {
        try
        {
            await DeferAsync();
            commandLogger.LogCommandCalled(Context);

            _ = Task.Run((Func<Task>)(async () =>
            {
                var allSongs = playerState.GetAllAvailableSongs();

                foreach (var song in allSongs.Where(song => string.IsNullOrEmpty(song.SpotifyId)))
                {
                    await audioService.SearchSpotifyAndUpdateAsync(song);

                    await Task.Delay(5000);
                }
            }));
            
            await MessageHelper.EmbedFollowupAsync(Context, "Searching for songs with missing spotifyIds and adding them to a playlist...", true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
