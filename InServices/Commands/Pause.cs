using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Pause(
    PlayerHolder playerHolder,
    ILogger<Pause> logger, 
    ICommandLogger<Pause> commandLogger,
    ValidationHelper validationHelper) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("pause", "Pauses/Plays the current song.")]
    public async Task PauseAsync()
    {
        await DeferAsync();
        commandLogger.LogCommandCalled(Context);
        
        var player = await validationHelper.ValidateAndGetPlayer(Context, logger, playerHolder);
        if (player == null)
            return;

        var currentSong = player.CurrentSong;
        var isPlaying = player.Pause();
        
        await MessageHelper.EmbedFollowupAsync(Context, $"{(isPlaying ? "Playing" : "Paused")} {currentSong!.Title} - {currentSong.Artist} | {currentSong.FormattedDuration()}", false);
    }
}
