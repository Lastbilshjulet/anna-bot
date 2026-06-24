using System;
using System.Linq;
using System.Threading.Tasks;
using anna_bot.InServices.Commands.Helpers;
using anna_bot.InServices.Models;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace anna_bot.InServices.Commands;

public class Disconnect(
    ILogger<Disconnect> logger, 
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
            await FollowupAsync("You are not connected to a voice channel.", ephemeral: true);
            return;
        }
        
        var audioClient = Context.Guild.AudioClient;
        if (audioClient == null)
        {
            await FollowupAsync("I am not connected to a voice channel.", ephemeral: true);
            return;
        }

        var usersInChannel = voiceChannel.Users.Select(x => x.Id);
        if (!usersInChannel.Contains(discordConfig.Value.ClientId))
        {
            await FollowupAsync("We need to be connected to the same channel for you to disconnect me.", ephemeral: true);
            return;
        }

        try
        {
            logger.LogInformation("Disconnecting from voice channel {VoiceChannelName} ({VoiceChannelId})", voiceChannel.Name, voiceChannel.Id);
            await voiceChannel.DisconnectAsync();

            await FollowupAsync($"Disconnecting from {voiceChannel.Name}...", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError("Error disconnecting to voice channel: {ExMessage}", ex.Message);
            await FollowupAsync("Failed to disconnect from your voice channel.", ephemeral: true);
        }
    }
}
