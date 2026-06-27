using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace anna_bot.OutServices.DbContexts;

[Table("songs")]
[Index(nameof(YoutubeId), IsUnique = true)]
public class SongEntity
{
    [Key]
    [Column("Id", TypeName = "INTEGER")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [MaxLength(255)]
    [Column("ytId", TypeName = "VARCHAR(255)")]
    public string YoutubeId { get; set; } = null!;
    
    [MaxLength(255)]
    [Column("title", TypeName = "VARCHAR(255)")]
    public string Title { get; set; } = null!;
    
    [MaxLength(255)]
    [Column("artist", TypeName = "VARCHAR(255)")]
    public string Artist { get; set; } = null!;
    
    [MaxLength(255)]
    [Column("thumbnail", TypeName = "VARCHAR(255)")]
    public string Thumbnail { get; set; } = null!;
    
    [MaxLength(255)]
    [Column("source", TypeName = "VARCHAR(255)")]
    public string Source { get; set; } = null!;
    
    [MaxLength(255)]
    [Column("path", TypeName = "VARCHAR(255)")]
    public string Path { get; set; } = null!;
    
    [MaxLength(10)]
    [Column("extension", TypeName = "VARCHAR(10)")]
    public string Extension { get; set; } = null!;
    
    [MaxLength(255)]
    [Column("requestedBy", TypeName = "VARCHAR(255)")]
    public string RequestedBy { get; set; } = null!;
    
    [Column("duration", TypeName = "INTEGER")]
    public long DurationSeconds { get; set; }
    
    [NotMapped]
    public TimeSpan Duration
    {
        get => TimeSpan.FromSeconds(DurationSeconds);
        set => DurationSeconds = (long)value.TotalSeconds;
    }
    
    [Column("timesPlayed", TypeName = "INTEGER")]
    public int TimesPlayed { get; set; }
    
    [Column("timesAutoPlayed", TypeName = "INTEGER")]
    public int TimesAutoPlayed { get; set; }
    
    [Column("autoplay", TypeName = "TINYINT(1)")]
    public bool Autoplay { get; set; } = true;
    
    [Column("createdAt", TypeName = "DATETIME")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    [Column("updatedAt", TypeName = "DATETIME")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
