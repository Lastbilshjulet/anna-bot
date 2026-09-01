using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ComponentInteractions;

public class PlaySongInteraction(PlayerState playerState, ILogger<PlaySongInteraction> logger)
    : ComponentInteractionBase(playerState, logger)
{
    [ComponentInteraction("PlaySongInteraction")]
    public async Task PlaySongInteractionAsync()
    {
        await HandleAsync(async player =>
        {
            if (!player.IsPlaying)
                player.Pause();

            await MessageHelper.EmbedSendMessageAsync(Context, player, logger);
        });
    }
}
