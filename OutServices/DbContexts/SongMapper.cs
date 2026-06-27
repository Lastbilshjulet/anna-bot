using System.Collections.Generic;
using anna_bot.Domain.Models;
using Riok.Mapperly.Abstractions;

namespace anna_bot.OutServices.DbContexts;

[Mapper]
public partial class SongMapper
{
    [MapperIgnoreSource(nameof(Song.IsAutoPlayed))]
    [MapperIgnoreTarget(nameof(SongEntity.Id))]
    [MapperIgnoreTarget(nameof(SongEntity.CreatedAt))]
    [MapperIgnoreTarget(nameof(SongEntity.UpdatedAt))]
    [MapperIgnoreTarget(nameof(SongEntity.DurationSeconds))]
    public partial SongEntity ToEntity(Song song);
    
    [MapperIgnoreTarget(nameof(Song.IsAutoPlayed))]
    [MapperIgnoreSource(nameof(SongEntity.Id))]
    [MapperIgnoreSource(nameof(SongEntity.CreatedAt))]
    [MapperIgnoreSource(nameof(SongEntity.UpdatedAt))]
    [MapperIgnoreSource(nameof(SongEntity.DurationSeconds))]
    public partial Song ToDomain(SongEntity entity);
    public partial List<Song> ToDomain(List<SongEntity> entity);
}
