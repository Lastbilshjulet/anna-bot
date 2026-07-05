using System;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Clear(
    PlayerState playerState,
    ILogger<Clear> logger, 
    ICommandLogger<Clear> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("clear", "Make the last song in the queue cut in line to be played next.")]
    public async Task ClearAsync()
    {
        try
        {
            await DeferAsync();
            commandLogger.LogCommandCalled(Context);
            
            var player = await ValidationHelper.ValidateAndGetPlayer(Context, logger, playerState);
            if (player == null)
                return;

            if (player.Queue.Count == 0)
            {
                await MessageHelper.EmbedFollowupAsync(Context, "No queue to clear.", true);
                return;
            }

            player.Queue.Clear();
            await MessageHelper.EmbedFollowupAsync(Context, "Cleared the queue.", false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
