using System.ComponentModel.DataAnnotations;

namespace anna_bot.Domain.Models.Configurations;

public class SpotifyConfiguration
{
    [Required]
    public string ClientId { get; set; } = null!;

    [Required]
    public string ClientSecret { get; set; } = null!;

    [Required]
    public string PlaylistId { get; set; } = null!;

    [Required]
    public string RefreshToken { get; set; } = null!;
}
