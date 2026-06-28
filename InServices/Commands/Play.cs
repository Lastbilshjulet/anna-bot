using System;
using System.Linq;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.Domain.Services;
using anna_bot.InServices.Commands.Autocompleters;
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
    [SlashCommand("play", "Plays song from query!")]
    public async Task PlayAsync([Autocomplete(typeof(SongAutocompleteHandler))] string query)
    {
        await DeferAsync(ephemeral: true);
        commandLogger.LogCommandCalled(Context, query);
        
        var guildUser = Context.User as SocketGuildUser;
        var voiceChannel = guildUser?.VoiceChannel;

        if (voiceChannel == null)
        {
            await MessageHelper.EmbedFollowupAsync(Context, "You are not connected to a voice channel.", true);
            return;
        }

        if (Context.Interaction.Channel is not SocketTextChannel textChannel)
        {
            await MessageHelper.EmbedFollowupAsync(Context, "You are typing from an invalid text channel.", true);
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
                await MessageHelper.EmbedFollowupAsync(Context, "Failed to connect to your voice channel.", true);
                return;
            }
        }

        try
        {
            var selectedSong = playerHolder.GetAllAvailableSongs().FirstOrDefault(x => x.YoutubeId == query);
            if (selectedSong == null)
            {
                selectedSong = await audioService.SearchAndFetch(query, guildUser);
                if (selectedSong == null)
                {
                    logger.LogInformation("No result found from query {Query}", query);
                    await MessageHelper.EmbedFollowupAsync(Context, $"No result found from query {query}", true);
                    return;
                }
            }

            logger.LogInformation("Adding song {SongName} to the queue in {VoiceChannelName} ({VoiceChannelId})", selectedSong.Title, voiceChannel.Name, voiceChannel.Id);
            playerHolder.AddSong(Context.Guild.Id, selectedSong, textChannel, voiceChannel);
            
            await MessageHelper.EmbedFollowupAsync(Context, $"Added {selectedSong.Title} to the queue.", true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching and playing song");
            await MessageHelper.EmbedFollowupAsync(Context, "Failed to fetch or play song.", true);
        }
    }
}
