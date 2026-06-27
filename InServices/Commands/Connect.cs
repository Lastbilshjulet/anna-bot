using System;
using System.Threading.Tasks;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Connect(ILogger<Connect> logger, ICommandLogger<Connect> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("connect", "Connects to your voice channel!")]
    public async Task ConnectAsync()
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
        if (audioClient != null)
        {
            await FollowupAsync("I am already connected to a voice channel.", ephemeral: true);
            return;
        }

        try
        {
            logger.LogInformation("Connecting to voice channel {VoiceChannelName} ({VoiceChannelId})", voiceChannel.Name, voiceChannel.Id);
            await voiceChannel.ConnectAsync();

            await FollowupAsync($"Connected to {voiceChannel.Name}", ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError("Error connecting to voice channel: {ExMessage}", ex.Message);
            await FollowupAsync("Failed to connect to your voice channel.", ephemeral: true);
        }
    }
}
