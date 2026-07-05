using System.Threading.Tasks;
using anna_bot.Domain.Models;
using Discord.WebSocket;

namespace anna_bot.Domain.Services;

public interface IAudioService
{
    Task<Song?> SearchAndFetchAsync(string query, SocketGuildUser? guildUser);
    Task<Song?> SearchSpotifyAndUpdateAsync(Song song);
}
