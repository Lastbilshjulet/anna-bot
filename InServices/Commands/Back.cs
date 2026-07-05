using System;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Back(
    PlayerState playerState,
    ILogger<Back> logger, 
    ICommandLogger<Back> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("back", "Goes back to the previously played song.")]
    public async Task BackAsync()
    {
        try
        {
            await DeferAsync();
            commandLogger.LogCommandCalled(Context);
        
            var player = await ValidationHelper.ValidateAndGetPlayer(Context, logger, playerState);
            if (player == null)
                return;

            await player.PlayPreviousSong();
        
            await MessageHelper.EmbedFollowupAsync(Context, "Going back to previous song!", false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
