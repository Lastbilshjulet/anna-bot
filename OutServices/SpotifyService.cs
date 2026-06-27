using System.Collections.Generic;
using System.Threading.Tasks;
using anna_bot.Domain.Models;
using anna_bot.OutServices.UseCases;

namespace anna_bot.OutServices;

public class SpotifyService : ISpotifyService
{
    public bool ValidateTrackUri(string uri)
    {
        return false;
    }

    public bool ValidatePlaylistUri(string uri)
    {
        return false;
    }

    public async Task<Song?> GetTrackDetails(string uri)
    {
        Song? song = null;
        return await Task.FromResult(song);
    }

    public async Task<List<Song>?> GetPlaylistDetails(string uri)
    {
        List<Song>? songs = null;
        return await Task.FromResult(songs);
    }
}
