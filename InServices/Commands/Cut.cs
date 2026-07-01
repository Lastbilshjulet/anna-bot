using System;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Cut(
    PlayerHolder playerHolder,
    ILogger<Cut> logger, 
    ICommandLogger<Cut> commandLogger,
    ValidationHelper validationHelper) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("cut", "Make the last song in the queue cut in line to be played next.")]
    public async Task CutAsync()
    {
        try
        {
            await DeferAsync();
            commandLogger.LogCommandCalled(Context);
        
            var player = await validationHelper.ValidateAndGetPlayer(Context, logger, playerHolder);
            if (player == null)
                return;

            if (player.Queue.Count < 2)
            {
                await MessageHelper.EmbedFollowupAsync(Context, "No queue to cut.", true);
                return;
            }

            var cutSong = player.Queue.Cut();
            await MessageHelper.EmbedFollowupAsync(Context, $"Moved {cutSong.Title} - {cutSong.Artist} to the front of the queue.", false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
