using System.Threading.Tasks;
using anna_bot.Domain.Models;
using Discord.WebSocket;

namespace anna_bot.Domain.Services;

public interface IAudioService
{
    Task<Song?> SearchAndFetch(string query, SocketGuildUser? guildUser);
}
