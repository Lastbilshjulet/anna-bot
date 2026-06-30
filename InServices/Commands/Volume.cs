using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Volume(
    PlayerHolder playerHolder,
    ILogger<Volume> logger, 
    ICommandLogger<Volume> commandLogger,
    ValidationHelper validationHelper) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("volume", "Responds with current volume, or sets a new value for the current song.")]
    public async Task VolumeAsync(float? volume = null)
    {
        await DeferAsync(ephemeral: true);
        commandLogger.LogCommandCalled(Context);
        
        var player = await validationHelper.ValidateAndGetPlayer(Context, logger, playerHolder);
        if (player == null)
            return;

        var currentlySetVolume = player.Volume;
        logger.LogInformation("Volume is currently set to: {Volume} on {SongTitle}", currentlySetVolume, player.CurrentSong!.Title);

        if (!volume.HasValue)
        {
            await MessageHelper.EmbedFollowupAsync(Context, $"Volume is set to: {player.DisplayVolume}", false);
            return;
        }
        
        player.Volume = volume.Value / 100;
        await MessageHelper.EmbedFollowupAsync(Context, $"Volume is now set to: {player.DisplayVolume}", false);
    }
}
