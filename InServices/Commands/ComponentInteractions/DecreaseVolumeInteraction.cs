using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ComponentInteractions;

public class DecreaseVolumeInteraction(PlayerState playerState, ILogger<DecreaseVolumeInteraction> logger)
    : ComponentInteractionBase(playerState, logger)
{
    [ComponentInteraction("DecreaseVolumeInteraction")]
    public async Task DecreaseVolumeInteractionAsync()
    {
        await HandleAsync(async player =>
        {
            player.DecreaseVolume();
            await MessageHelper.EmbedSendMessageAsync(Context, player, logger);
        });
    }
}
