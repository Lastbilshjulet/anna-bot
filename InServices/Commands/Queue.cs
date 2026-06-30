using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Queue(
    PlayerHolder playerHolder,
    ILogger<Queue> logger, 
    ICommandLogger<Queue> commandLogger,
    ValidationHelper validationHelper) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("queue", "Displays the current queue.")]
    public async Task QueueAsync()
    {
        await DeferAsync();
        commandLogger.LogCommandCalled(Context);
        
        var player = await validationHelper.ValidateAndGetPlayer(Context, logger, playerHolder);
        if (player == null)
            return;

        if (player.Queue.Count == 0)
        {
            await MessageHelper.EmbedFollowupAsync(Context, "No queue to display.", true);
            return;
        }

        var queuedSongs = player.Queue.GetQueue;
        
        await MessageHelper.EmbedFollowupAsync(Context, $"Queue - {player.Queue.Count} total", player.CurrentSong, queuedSongs);
    }
}
