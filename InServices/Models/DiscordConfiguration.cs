using System.ComponentModel.DataAnnotations;

namespace anna_bot.InServices.Models;

public class DiscordConfiguration
{
    [Required] public string Token { get; set; } = null!;
    [Required] public string OwnerId { get; set; } = null!;
}
