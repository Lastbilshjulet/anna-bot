using System.Threading.Tasks;
using anna_bot.Domain.Models;

namespace anna_bot.OutServices.UseCases;

public interface IDiscordDmService
{
    Task SendSpotifySongMismatchNotification(Song song, Song spotifySong);
}
