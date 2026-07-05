using System;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Skip(
    PlayerState playerState,
    ILogger<Skip> logger, 
    ICommandLogger<Skip> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("skip", "Skips the currently playing song.")]
    public async Task SkipAsync()
    {
        try
        {
            await DeferAsync();
            commandLogger.LogCommandCalled(Context);
        
            var player = await ValidationHelper.ValidateAndGetPlayer(Context, logger, playerState);
            if (player == null)
                return;

            var currentSong = player.CurrentSong;
            await player.Skip();
        
            await MessageHelper.EmbedFollowupAsync(Context, $"Skipping {currentSong!.Title} - {currentSong.Artist}!", false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
