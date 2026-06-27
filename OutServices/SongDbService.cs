using System;
using System.Collections.Generic;
using System.Linq;
using anna_bot.Domain.Models;
using anna_bot.OutServices.DbContexts;
using anna_bot.OutServices.UseCases;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace anna_bot.OutServices;

public class SongDbService(
    IDbContextFactory<SongDbContext> dbContextFactory, 
    SongMapper mapper,
    ILogger<SongDbService> logger) : ISongDbService
{
    public List<Song> GetAllSongs()
    {
        using var context = dbContextFactory.CreateDbContext();
        return mapper.ToDomain(context.Songs.ToList());
    }

    public Song? GetSongByYtId(string ytId)
    {
        using var context = dbContextFactory.CreateDbContext();
        var song = context.Songs.FirstOrDefault(x => x.YoutubeId == ytId);

        return song == null ? null : mapper.ToDomain(song);
    }

    public Song InsertSong(Song song)
    {
        using var context = dbContextFactory.CreateDbContext();
        var entity = mapper.ToEntity(song);
        
        context.Songs.Add(entity);
        context.SaveChanges();
        
        return mapper.ToDomain(entity);
    }

    public Song IncreasePlayAmount(Song song)
    {
        using var context = dbContextFactory.CreateDbContext();
        var dbSong = context.Songs.FirstOrDefault(x => x.YoutubeId == song.YoutubeId);
        if (dbSong == null)
        {
            logger.LogCritical("Song to update was not found {SongTitle} ({YoutubeId})", song.Title, song.YoutubeId);
            throw new Exception($"Song not found, should never happen");
        }

        if (song.IsAutoPlayed)
            dbSong.TimesAutoPlayed++;
        else
            dbSong.TimesPlayed++;
        
        dbSong.UpdatedAt = DateTime.Now;
        
        context.SaveChanges();
        
        return mapper.ToDomain(dbSong);
    }
}
