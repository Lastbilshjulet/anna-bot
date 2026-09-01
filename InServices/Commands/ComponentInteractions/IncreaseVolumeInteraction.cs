using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ComponentInteractions;

public class IncreaseVolumeInteraction(PlayerState playerState, ILogger<IncreaseVolumeInteraction> logger)
    : ComponentInteractionBase(playerState, logger)
{
    [ComponentInteraction("IncreaseVolumeInteraction")]
    public async Task IncreaseVolumeInteractionAsync()
    {
        await HandleAsync(async player =>
        {
            player.IncreaseVolume();
            await MessageHelper.EmbedSendMessageAsync(Context, player, logger);
        });
    }
}
