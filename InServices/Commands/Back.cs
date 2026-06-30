using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Back(
    PlayerHolder playerHolder,
    ILogger<Back> logger, 
    ICommandLogger<Back> commandLogger,
    ValidationHelper validationHelper) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("back", "Goes back to the previously played song.")]
    public async Task BackAsync()
    {
        await DeferAsync();
        commandLogger.LogCommandCalled(Context);
        
        var player = await validationHelper.ValidateAndGetPlayer(Context, logger, playerHolder);
        if (player == null)
            return;

        await player.PlayPreviousSong();
        
        await MessageHelper.EmbedFollowupAsync(Context, "Going back to previous song!", false);
    }
}
