using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using anna_bot.Domain.Models;
using anna_bot.Domain.Models.Configurations;
using anna_bot.Domain.Services;
using anna_bot.OutServices.UseCases;
using Discord.Audio;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace anna_bot.Domain;

public class PlayerState(ISongDbService songDbService, IOptions<MusicConfiguration> musicConfig, ILoggerFactory loggerFactory)
{
    private readonly ConcurrentDictionary<ulong, Player> _playerHolder = [];
    private List<Song> _availableSongs = songDbService.GetAllSongs();

    public Player AddAndGetPlayer(ulong guildId, IAudioClient audioClient)
    {
        var logger = loggerFactory.CreateLogger<Player>();
        return _playerHolder.GetOrAdd(guildId, new Player(
            songDbService, 
            musicConfig.Value, 
            guildId, 
            audioClient, 
            [.. _availableSongs], 
            () => RemovePlayer(guildId), 
            logger));
    }
    
    public void AddSong(ulong guildId, Song song, SocketTextChannel textChannel, SocketVoiceChannel voiceChannel)
    {
        var newSong = _availableSongs.All(x => x.YoutubeId != song.YoutubeId);
        if (newSong)
            _availableSongs.Add(song);
        
        foreach (var player in GetAllExistingPlayers())
        {
            if (player.GuildId == guildId)
            {
                player.Enqueue(song, textChannel, voiceChannel);
            }
            else if (newSong)
            {
                player.Queue.AddUnplayed(song);
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

    private Task RemovePlayer(ulong guildId)
    {
        _playerHolder.TryRemove(guildId, out _);
        return Task.CompletedTask;
    }

    public void Refresh()
    {
        _availableSongs = songDbService.GetAllSongs();
    }

    public async Task ReconnectPlayers()
    {
        foreach (var player in GetAllExistingPlayers())
        {
            await player.Reconnect();
        }
    }
}
