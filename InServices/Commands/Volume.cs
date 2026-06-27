using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Volume(
    PlayerHolder playerHolder,
    ILogger<Volume> logger, 
    ICommandLogger<Volume> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("volume", "Responds with current volume, or sets a new value for the current song.")]
    public async Task VolumeAsync(float? volume = null)
    {
        await DeferAsync(ephemeral: true);
        commandLogger.LogCommandCalled(Context);
        
        var guildUser = Context.User as SocketGuildUser;
        var voiceChannel = guildUser?.VoiceChannel;

        if (voiceChannel == null)
        {
            await MessageHelper.EmbedFollowup(Context, "You are not connected to a voice channel.", true);
            return;
        }

        var player = playerHolder.GetExistingPlayer(Context.Guild.Id);
        if (player is not { IsPlaying: true })
        {
            logger.LogError("Player not found for guild {GuildId}", Context.Guild.Id);
            await MessageHelper.EmbedFollowup(Context, "No music playing.", true);
            return;
        }

        var currentlySetVolume = player.Volume;
        logger.LogInformation("Volume is currently set to: {Volume} on {SongTitle}", currentlySetVolume, player.CurrentSong?.Title ?? "Unknown");

        if (!volume.HasValue)
        {
            await MessageHelper.EmbedFollowup(Context, $"Volume is set to: {currentlySetVolume * 100}%", true);
            return;
        }
        
        player.Volume = volume.Value / 100;
        await MessageHelper.EmbedFollowup(Context, $"Volume is now set to: {volume.Value}%", true);
    }
}
