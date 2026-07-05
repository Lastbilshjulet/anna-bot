using System;
using System.Collections.Generic;
using System.Linq;

namespace anna_bot.Domain.Models;

public class SongQueue
{
    private readonly LinkedList<Song> _queue = [];
    private readonly List<Song> _unPlayed = [];
    private readonly List<Song> _history = [];
    
    public int Count => _queue.Count;
    private int UnPlayedCount => _unPlayed.Count;
    public int HistoryCount => _history.Count;
    public List<Song> GetQueue => _queue.ToList();
    public List<Song> GetHistory => _history.ToList();
    
    public SongQueue(List<Song> existingSongs)
    {
        _unPlayed.AddRange(existingSongs.Where(s => s.Autoplay));
    }
    
    public void Enqueue(Song song)
    {
        _queue.AddLast(song);
    }

    public void AddUnplayed(Song song)
    {
        _unPlayed.Add(song);
    }

    public Song? Dequeue()
    {
        if (Count > 0)
        {
            var song = _queue.First();
            song.IsAutoPlayed = false;
            _history.Add(song);
            _queue.RemoveFirst();
            _unPlayed.Remove(song);
            return song;
        }

        if (UnPlayedCount > 0)
        {
            var song = _unPlayed.ElementAt(Random.Shared.Next(_unPlayed.Count));
            song.IsAutoPlayed = true;
            _unPlayed.Remove(song);
            _history.Add(song);
            return song;
        }

        return null;
    }
    
    public void QueueFromHistoryFirst()
    {
        if (HistoryCount <= 1)
            return;
        
        _queue.AddFirst(_history[^2]);
    }

    public void QueueSameSongFirst()
    {
        if (HistoryCount == 0)
            return;
        
        _queue.AddFirst(_history[^1]);
    }

    public void Clear()
    {
        var unplayedSongs = _unPlayed.Select(x => x.YoutubeId);
        foreach (var unplayedFromQueue in _queue.Where(unplayedFromQueue => unplayedSongs.Contains(unplayedFromQueue.YoutubeId)))
        {
            AddUnplayed(unplayedFromQueue);
        }
        _queue.Clear();
    }

    public Song Cut()
    {
        var lastSong = _queue.Last();
        _queue.RemoveLast();
        _queue.AddFirst(lastSong);

        return lastSong;
    }
}
