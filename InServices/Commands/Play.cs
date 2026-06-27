using System;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.Domain.Services;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Play(
    PlayerHolder playerHolder, 
    IAudioService audioService, 
    ILogger<Play> logger, 
    ICommandLogger<Play> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    // TODO: Add buttons to skip etc
    // TODO: Add autocomplete for existing songs
    [SlashCommand("play", "Plays song from query!")]
    public async Task PlayAsync(string query)
    {
        await DeferAsync(ephemeral: true);
        commandLogger.LogCommandCalled(Context, query);
        
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
            try
            {
                logger.LogInformation("Connecting to voice channel {VoiceChannelName} ({VoiceChannelId})", voiceChannel.Name, voiceChannel.Id);
                audioClient = await voiceChannel.ConnectAsync();
        
                playerHolder.AddAndGetPlayer(Context.Guild.Id, audioClient);
            }
            catch (Exception ex)
            {
                logger.LogError("Error connecting to voice channel: {ExMessage}", ex.Message);
                await FollowupAsync("Failed to connect to your voice channel.", ephemeral: true);
                return;
            }
        }

        try
        {
            var fetchedSong = await audioService.SearchAndFetch(query, guildUser);
            if (fetchedSong == null)
            {
                logger.LogInformation("No result found from query {Query}", query);
                await FollowupAsync($"No result found from query {query}", ephemeral: true);
                return;
            }

            logger.LogInformation("Adding song {SongName} to the queue in {VoiceChannelName} ({VoiceChannelId})", fetchedSong.Title, voiceChannel.Name, voiceChannel.Id);
            playerHolder.AddSong(Context.Guild.Id, fetchedSong);
            
            await FollowupAsync($"Added {fetchedSong.Title} to the queue.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching and playing song");
            await FollowupAsync("Failed to fetch or play song.", ephemeral: true);
        }
    }
}
