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
        await DeferAsync(ephemeral: true);
        commandLogger.LogCommandCalled(Context);
        
        var player = await validationHelper.ValidateAndGetPlayer(Context, logger, playerHolder);
        if (player == null)
            return;

        try
        {
            var voiceChannel = player.VoiceChannel;
            logger.LogInformation("Disconnecting from voice channel {VoiceChannelName} ({VoiceChannelId})", voiceChannel!.Name, voiceChannel.Id);
            
            await player.DisconnectAsync();

            await MessageHelper.EmbedFollowupAsync(Context, $"I was disconnected from {voiceChannel.Name} by {(Context.User as SocketGuildUser)!.DisplayName}", false);
        }
        catch (Exception ex)
        {
            logger.LogError("Error disconnecting to voice channel: {ExMessage}", ex.Message);
            await MessageHelper.EmbedFollowupAsync(Context, "Failed to disconnect from your voice channel.", true);
        }
    }
}
