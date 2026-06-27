using System;
using System.Linq;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.Domain.Models.Configurations;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace anna_bot.InServices.Commands;

public class Disconnect(
    ILogger<Disconnect> logger, 
    PlayerHolder playerHolder,
    ICommandLogger<Disconnect> commandLogger,
    IOptions<DiscordConfiguration> discordConfig) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("disconnect", "Disconnects bot from your voice channel!")]
    public async Task DisconnectAsync()
    {
        await DeferAsync(ephemeral: true);
        commandLogger.LogCommandCalled(Context);
        
        var guildUser = Context.User as SocketGuildUser;
        var voiceChannel = guildUser?.VoiceChannel;

        if (voiceChannel == null)
        {
            await MessageHelper.EmbedFollowupAsync(Context, "You are not connected to a voice channel.", true);
            return;
        }
        
        var audioClient = Context.Guild.AudioClient;
        if (audioClient == null)
        {
            await MessageHelper.EmbedFollowupAsync(Context, "I am not connected to a voice channel.", true);
            return;
        }

        var usersInChannel = voiceChannel.ConnectedUsers.Select(x => x.Id);
        if (!usersInChannel.Contains(discordConfig.Value.ClientId))
        {
            await MessageHelper.EmbedFollowupAsync(Context, "We need to be connected to the same channel for you to disconnect me.", true);
            return;
        }

        try
        {
            logger.LogInformation("Disconnecting from voice channel {VoiceChannelName} ({VoiceChannelId})", voiceChannel.Name, voiceChannel.Id);
            playerHolder.RemovePlayer(Context.Guild.Id);
            await voiceChannel.DisconnectAsync();

            await MessageHelper.EmbedFollowupAsync(Context, $"Disconnected from {voiceChannel.Name}", true);
        }
        catch (Exception ex)
        {
            logger.LogError("Error disconnecting to voice channel: {ExMessage}", ex.Message);
            await MessageHelper.EmbedFollowupAsync(Context, "Failed to disconnect from your voice channel.", true);
        }
    }
}
