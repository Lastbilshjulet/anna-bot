using System.ComponentModel.DataAnnotations;

namespace anna_bot.Domain.Models.Configurations;

public class MusicConfiguration
{
    [Required]
    public string Path { get; set; } = null!;

    [Required]
    public string Extension { get; set; } = null!;

    [Required]
    public float BaseVolume { get; set; } = 0.1f;
}
