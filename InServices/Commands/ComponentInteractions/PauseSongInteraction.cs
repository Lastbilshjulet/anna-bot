using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ComponentInteractions;

public class PauseSongInteraction(PlayerState playerState, ILogger<PauseSongInteraction> logger)
    : ComponentInteractionBase(playerState, logger)
{
    [ComponentInteraction("PauseSongInteraction")]
    public async Task PauseSongInteractionAsync()
    {
        await HandleAsync(async player =>
        {
            if (player.IsPlaying)
                player.Pause();

            await MessageHelper.EmbedSendMessageAsync(Context, player, logger);
        });
    }
}
