using System;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Seek(
    PlayerState playerState,
    ILogger<Seek> logger, 
    ICommandLogger<Seek> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("seek", "Seek to the desired position in the currently playing song.")]
    public async Task SeekAsync(int seconds)
    {
        try
        {
            await DeferAsync(ephemeral: true);
            commandLogger.LogCommandCalled(Context);

            if (seconds < 0)
            {
                await MessageHelper.EmbedFollowupAsync(Context, "Seek value must not be negative.", true);
                return;
            }
        
            var player = await ValidationHelper.ValidateAndGetPlayer(Context, logger, playerState);
            if (player == null)
                return;

            if (player.CurrentSong == null)
            {
                await MessageHelper.EmbedFollowupAsync(Context, "No song is currently playing.", true);
                return;
            }

            if (player.CurrentSong.Duration.TotalSeconds - 10 < seconds)
            {
                await MessageHelper.EmbedFollowupAsync(Context, "Can't seek to the end of the song.", true);
                return;
            }
            
            await player.Seek(seconds);
            
            await MessageHelper.EmbedFollowupAsync(Context, $"Seeked to {seconds} seconds.", true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
