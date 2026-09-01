using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using anna_bot.OutServices.UseCases;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ComponentInteractions;

public class ToggleAutoplayInteraction(
    ISongDbService songDbService,
    PlayerState playerState,
    ILogger<ToggleAutoplayInteraction> logger) : ComponentInteractionBase(playerState, logger)
{
    [ComponentInteraction("ToggleAutoplayInteraction")]
    public async Task ToggleAutoplayInteractionAsync()
    {
        await HandleAsync(async player =>
        {
            if (player.CurrentSong == null)
            {
                await DeferAsync();
                await MessageHelper.EmbedFollowupAsync(Context, "Song is null, can't toggle auto play", false);
                return;
            }
            
            player.CurrentSong!.Autoplay = !player.CurrentSong.Autoplay;
            await MessageHelper.EmbedSendMessageAsync(Context, player, logger);
            await songDbService.ToggleAutoplayAsync(player.CurrentSong);
        });
    }
}
