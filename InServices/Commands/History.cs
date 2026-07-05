using System;
using System.Linq;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class History(
    PlayerState playerState,
    ILogger<History> logger, 
    ICommandLogger<History> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("history", "Displays the current history.")]
    public async Task HistoryAsync()
    {
        try
        {
            await DeferAsync();
            commandLogger.LogCommandCalled(Context);
        
            var player = await ValidationHelper.ValidateAndGetPlayer(Context, logger, playerState);
            if (player == null)
                return;

            if (player.Queue.HistoryCount <= 1 )
            {
                await MessageHelper.EmbedFollowupAsync(Context, "No history to display.", true);
                return;
            }

            var historySongs = player.Queue.GetHistory;
            historySongs.Reverse();
            historySongs = historySongs.Skip(1).ToList();
        
            await MessageHelper.EmbedFollowupAsync(Context, $"History - {player.Queue.HistoryCount - 1} total", player.CurrentSong, historySongs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
