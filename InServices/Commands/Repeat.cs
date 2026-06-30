using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Repeat(
    PlayerHolder playerHolder,
    ILogger<Repeat> logger, 
    ICommandLogger<Repeat> commandLogger,
    ValidationHelper validationHelper) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("repeat", "Repeats the currently playing song.")]
    public async Task RepeatAsync()
    {
        await DeferAsync();
        commandLogger.LogCommandCalled(Context);
        
        var player = await validationHelper.ValidateAndGetPlayer(Context, logger, playerHolder);
        if (player == null)
            return;

        var currentSong = player.CurrentSong;

        var repeat = player.ToggleRepeat();
        
        await MessageHelper.EmbedFollowupAsync(Context, $"{(repeat ? "Repeating" : "Unrepeating")} {currentSong!.Title} - {currentSong.Artist} | {currentSong.FormattedDuration()}", false);
    }
}
