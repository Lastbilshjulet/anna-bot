using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.Domain.Models;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.Helpers;

public class ValidationHelper
{
    public async Task<Player?> ValidateAndGetPlayer(SocketInteractionContext context, ILogger logger, PlayerHolder playerHolder)
    {
        var guildUser = context.User as SocketGuildUser;
        var voiceChannel = guildUser?.VoiceChannel;

        if (voiceChannel == null)
        {
            await MessageHelper.EmbedFollowupAsync(context, "You are not connected to a voice channel.", true);
            return null;
        }

        var player = playerHolder.GetExistingPlayer(context.Guild.Id);
        if (player?.CurrentSong is null)
        {
            logger.LogError("Player not found for guild {GuildId}", context.Guild.Id);
            await MessageHelper.EmbedFollowupAsync(context, "No music found.", true);
            return null;
        }

        if (voiceChannel.Id != player.VoiceChannel?.Id)
        {
            await MessageHelper.EmbedFollowupAsync(context, "We need to be connected to the same channel for you use this command.", true);
            return null;
        }
        
        return player;
    }
}
