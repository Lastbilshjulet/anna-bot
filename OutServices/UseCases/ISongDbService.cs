using System.Collections.Generic;
using System.Threading.Tasks;
using anna_bot.Domain.Models;

namespace anna_bot.OutServices.UseCases;

public interface ISongDbService
{
    List<Song> GetAllSongs();
    Song? GetSongByYtId(string ytId);
    Song InsertSong(Song song);
    Song IncreasePlayAmount(Song song);
}
