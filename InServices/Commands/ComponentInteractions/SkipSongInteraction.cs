using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ComponentInteractions;

public class SkipSongInteraction(PlayerState playerState, ILogger<SkipSongInteraction> logger)
    : ComponentInteractionBase(playerState, logger)
{
    [ComponentInteraction("SkipSongInteraction")]
    public async Task SkipSongInteractionAsync()
    {
        await HandleAsync(async player =>
        {
            await DeferAsync();
            var songToBeSkipped = "Skipping currently playing song!";
            if (player.CurrentSong != null)
                songToBeSkipped = $"Skipping {player.CurrentSong.Title} - {player.CurrentSong.Artist}!";
            await player.Skip();

            await MessageHelper.EmbedFollowupAsync(Context, songToBeSkipped, false);
        });
    }
}
