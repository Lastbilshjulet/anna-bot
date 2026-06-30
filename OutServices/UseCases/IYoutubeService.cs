using System.Threading.Tasks;
using anna_bot.Domain.Models;

namespace anna_bot.OutServices.UseCases;

public interface IYoutubeService
{
    bool ValidateVideoUri(string uri);
    bool ValidatePlaylistUri(string uri);
    Task<Song?> Search(string query, Song? song);
    Task<Song?> GetVideoDetails(string uri);
    Task<string?> DownloadSong(Song song);
}
