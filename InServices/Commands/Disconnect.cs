using System;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Disconnect(
    ILogger<Disconnect> logger, 
    PlayerHolder playerHolder,
    ICommandLogger<Disconnect> commandLogger,
    ValidationHelper validationHelper) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("disconnect", "Disconnects bot from your voice channel!")]
    public async Task DisconnectAsync()
    {
        try
        {
            await DeferAsync(ephemeral: true);
            commandLogger.LogCommandCalled(Context);
            
            var player = await validationHelper.ValidateAndGetPlayer(Context, logger, playerHolder);
            if (player == null)
                return;
            var voiceChannel = player.VoiceChannel;
            logger.LogInformation("Disconnecting from voice channel {VoiceChannelName} ({VoiceChannelId})", voiceChannel!.Name, voiceChannel.Id);
            
            await player.DisconnectAsync();

            await MessageHelper.EmbedFollowupAsync(Context, $"I was disconnected from {voiceChannel.Name} by {(Context.User as SocketGuildUser)!.DisplayName}", false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
