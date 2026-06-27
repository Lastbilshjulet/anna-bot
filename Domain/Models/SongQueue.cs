using System;
using System.Collections.Generic;
using System.Linq;

namespace anna_bot.Domain.Models;

public class SongQueue
{
    private readonly Queue<Song> _queue = new();
    private readonly List<Song> _unPlayed = [];
    private readonly Queue<Song> _history = new();
    
    public int Count => _queue.Count;
    public int UnPlayedCount => _unPlayed.Count;
    public int CombinedCount => Count + UnPlayedCount;
    public int HistoryCount => _history.Count;
    
    public SongQueue(List<Song> existingSongs)
    {
        _unPlayed.AddRange(existingSongs.Where(s => s.Autoplay));
    }
    
    public void Enqueue(Song song)
    {
        _queue.Enqueue(song);
    }

    public void AddUnplayed(Song song)
    {
        _unPlayed.Add(song);
    }

    public Song? Dequeue()
    {
        if (Count > 0)
        {
            var song = _queue.Dequeue();
            song.IsAutoPlayed = false;
            _history.Enqueue(song);
            _unPlayed.Remove(song);
            return song;
        }

        if (UnPlayedCount > 0)
        {
            var song = _unPlayed.ElementAt(Random.Shared.Next(_unPlayed.Count));
            song.IsAutoPlayed = true;
            _unPlayed.Remove(song);
            _history.Enqueue(song);
            return song;
        }

        return null;
    }

    public void Clear()
    {
        _queue.Clear();
    }
}
