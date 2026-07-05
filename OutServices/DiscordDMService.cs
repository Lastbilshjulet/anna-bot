using System;
using System.Threading.Tasks;
using anna_bot.Domain.Models;
using anna_bot.Domain.Models.Configurations;
using anna_bot.OutServices.UseCases;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace anna_bot.OutServices;

public class DiscordDmService(
    DiscordSocketClient discordClient, 
    IOptions<DiscordConfiguration> discordConfiguration, 
    ILogger<DiscordDmService> logger) : IDiscordDmService
{
    public async Task SendSpotifySongMismatchNotification(Song song, Song spotifySong)
    {
        try
        {
            var owner = await discordClient.GetUserAsync(discordConfiguration.Value.OwnerId);
            if (owner == null)
                return;

            var embed = new EmbedBuilder()
                .WithColor(0x0600ff)
                .WithTitle("Song mismatch")
                .WithTimestamp(DateTime.Now);

            embed.AddField($"Youtube: {song.GetYouTubeUrl()}", $"Spotify: {spotifySong.GetSpotifyUrl()}");
            
            await owner.SendMessageAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send DM to owner");
        }
    }
}
