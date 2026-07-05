using System;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Pause(
    PlayerState playerState,
    ILogger<Pause> logger, 
    ICommandLogger<Pause> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("pause", "Pauses/Plays the current song.")]
    public async Task PauseAsync()
    {
        try
        {
            await DeferAsync();
            commandLogger.LogCommandCalled(Context);
        
            var player = await ValidationHelper.ValidateAndGetPlayer(Context, logger, playerState);
            if (player == null)
                return;

            var currentSong = player.CurrentSong;
            var isPlaying = player.Pause();
        
            await MessageHelper.EmbedFollowupAsync(Context, $"{(isPlaying ? "Playing" : "Paused")} {currentSong!.Title} - {currentSong.Artist} | {currentSong.FormattedDuration()}", false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
