using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using anna_bot.Domain.Models;
using anna_bot.Domain.Models.Configurations;
using anna_bot.OutServices.UseCases;
using Discord.Audio;
using Microsoft.Extensions.Options;

namespace anna_bot.Domain;

// Find better name than PlayerHolder? Holds global state for music
public class PlayerHolder(ISongDbService songDbService, IOptions<MusicConfiguration> musicConfig)
{
    private readonly ConcurrentDictionary<ulong, Player> _playerHolder = [];
    private readonly List<Song> _availableSongs = songDbService.GetAllSongs();

    public Player AddAndGetPlayer(ulong guildId, IAudioClient audioClient)
    {
        return _playerHolder.GetOrAdd(guildId, new Player(songDbService, musicConfig.Value, guildId, audioClient, [.. _availableSongs]));
    }
    
    public void AddSong(ulong guildId, Song song)
    {
        var newSong = _availableSongs.All(x => x.YoutubeId != song.YoutubeId);
        if (newSong)
            _availableSongs.Add(song);
        
        foreach (var player in GetAllExistingPlayers())
        {
            if (player.GuildId == guildId)
            {
                player.Enqueue(song);
            }
            else if (newSong)
            {
                player.AddUnplayed(song);
            }
        }
    }

    public Player? GetExistingPlayer(ulong guildId)
    {
        _playerHolder.TryGetValue(guildId, out var value);
        return value;
    }

    private List<Player> GetAllExistingPlayers()
    {
        return [.. _playerHolder.Values];
    }

    public List<Song> GetAllAvailableSongs()
    {
        return [.. _availableSongs];
    }

    public void RemovePlayer(ulong guildId)
    {
        _playerHolder.TryRemove(guildId, out _);
    }
}
