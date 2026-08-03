using System;
using System.Threading.Tasks;
using anna_bot.Domain.Services;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class SyncPlaylist(
    IAudioService audioService,
    ILogger<SyncPlaylist> logger, 
    ICommandLogger<SyncPlaylist> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("syncplaylist", "Loops through and syncs songs in db with spotify playlist.")]
    public async Task SyncPlaylistAsync()
    {
        try
        {
            await DeferAsync(ephemeral: true);
            commandLogger.LogCommandCalled(Context);
            
            await MessageHelper.EmbedFollowupAsync(Context, "Syncing songs...", true);

            _ = Task.Run(async () =>
            {
                await audioService.SyncPlaylist();
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
