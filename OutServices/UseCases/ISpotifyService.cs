using System.Collections.Generic;
using System.Threading.Tasks;
using anna_bot.Domain.Models;

namespace anna_bot.OutServices.UseCases;

public interface ISpotifyService
{
    bool ValidateTrackUri(string uri);
    bool ValidatePlaylistUri(string uri);
    Task<Song?> GetTrackDetails(string uri);
    Task<List<Song>?> GetPlaylistDetails(string uri);
}
