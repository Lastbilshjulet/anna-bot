using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ComponentInteractions;

public class DisconnectPlayerInteraction(PlayerState playerState, ILogger<DisconnectPlayerInteraction> logger)
    : ComponentInteractionBase(playerState, logger)
{
    [ComponentInteraction("DisconnectPlayerInteraction")]
    public async Task DisconnectPlayerInteractionAsync()
    {
        await HandleAsync(async player =>
        {
            await DeferAsync();
            await player.DisconnectAsync();
            await MessageHelper.EmbedFollowupAsync(Context, "I was disconnected", false);
        });
    }
}
