using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ComponentInteractions;

public class BackSongInteraction(PlayerState playerState, ILogger<BackSongInteraction> logger)
    : ComponentInteractionBase(playerState, logger)
{
    [ComponentInteraction("BackSongInteraction")]
    public async Task BackSongInteractionAsync()
    {
        await HandleAsync(async player =>
        {
            await DeferAsync();
            await player.PlayPreviousSong();
            await MessageHelper.EmbedFollowupAsync(Context, "Going back to previous song!", false);
        });
    }
}
