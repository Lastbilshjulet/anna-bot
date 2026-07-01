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
        try
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
            if (audioClient != null)
            {
                await MessageHelper.EmbedFollowupAsync(Context, "I am already connected to a voice channel.", true);
                return;
            }
            
            logger.LogInformation("Connecting to voice channel {VoiceChannelName} ({VoiceChannelId})", voiceChannel.Name, voiceChannel.Id);
            await voiceChannel.ConnectAsync();

            await MessageHelper.EmbedFollowupAsync(Context, $"Connected to {voiceChannel.Name}", false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
