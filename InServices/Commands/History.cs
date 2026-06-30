using System.Linq;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class History(
    PlayerHolder playerHolder,
    ILogger<History> logger, 
    ICommandLogger<History> commandLogger,
    ValidationHelper validationHelper) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("history", "Displays the current history.")]
    public async Task HistoryAsync()
    {
        await DeferAsync();
        commandLogger.LogCommandCalled(Context);
        
        var player = await validationHelper.ValidateAndGetPlayer(Context, logger, playerHolder);
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
}
