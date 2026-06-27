using System.ComponentModel.DataAnnotations;

namespace anna_bot.Domain.Models.Configurations;

public class DiscordConfiguration
{
    [Required]
    public string BotName { get; set; } = null!;
    
    [Required]
    public string Token { get; set; } = null!;
    
    [Required]
    public ulong OwnerId { get; set; } = 0;
    
    [Required]
    public ulong GuildId { get; set; } = 0;
    
    [Required]
    public ulong ClientId { get; set; } = 0;
    
    [Required]
    public bool RemoveCommands { get; set; }
}
