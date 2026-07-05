using System;
using System.Threading.Tasks;
using anna_bot.Domain.Models.Configurations;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace anna_bot.InServices.Commands;

public class SpotifyPlaylist(
    IOptions<SpotifyConfiguration> spotifyConfiguration,
    ILogger<SpotifyPlaylist> logger, 
    ICommandLogger<SpotifyPlaylist> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("spotifyplaylist", "Returns the spotify playlist with the bots songs on it.")]
    public async Task SpotifyPlaylistAsync()
    {
        try
        {
            await DeferAsync(ephemeral: true);
            commandLogger.LogCommandCalled(Context);
        
            await MessageHelper.EmbedFollowupAsync(Context, $"The spotify playlist can be found on https://open.spotify.com/playlist/{spotifyConfiguration.Value.PlaylistId}", true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
