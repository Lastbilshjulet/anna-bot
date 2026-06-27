using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Skip(
    PlayerHolder playerHolder,
    ILogger<Skip> logger, 
    ICommandLogger<Skip> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("skip", "Skips the currently playing song.")]
    public async Task SkipAsync()
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

        var player = playerHolder.GetExistingPlayer(Context.Guild.Id);
        if (player is not { IsPlaying: true })
        {
            logger.LogError("Player not found for guild {GuildId}", Context.Guild.Id);
            await MessageHelper.EmbedFollowupAsync(Context, "No music playing to be skipped.", true);
            return;
        }

        var songToBeSkipped = "Skipping currently playing song!";
        if (player.CurrentSong != null)
            songToBeSkipped = $"Skipping {player.CurrentSong.Title} - {player.CurrentSong.Artist}!";
        player.Skip();
        
        await MessageHelper.EmbedFollowupAsync(Context, songToBeSkipped, false);
    }
}
