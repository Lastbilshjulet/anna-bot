using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ComponentInteractions;

public class RepeatSongInteraction(PlayerState playerState, ILogger<RepeatSongInteraction> logger)
    : ComponentInteractionBase(playerState, logger)
{
    [ComponentInteraction("RepeatSongInteraction")]
    public async Task RepeatSongInteractionAsync()
    {
        await HandleAsync(async player =>
        {
            player.ToggleRepeat();
            await MessageHelper.EmbedSendMessageAsync(Context, player, logger);
        });
    }
}
