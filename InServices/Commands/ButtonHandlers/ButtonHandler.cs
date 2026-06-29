using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.Domain.Models;
using anna_bot.InServices.Commands.Helpers;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ButtonHandlers;

public class ButtonHandler(PlayerHolder playerHolder, ILogger<ButtonHandler> logger)
{
    public async Task OnButtonExecuted(SocketMessageComponent component)
    {
        switch (component.Data.CustomId)
        {
            case "SkipButton":
                await SkipButtonHandler(component);
                break;
            case "DisconnectButton":
                await DisconnectButtonHandler(component);
                break;
        }
    }

    private async Task DisconnectButtonHandler(SocketMessageComponent component)
    {
        await component.DeferAsync();
        logger.LogInformation("Disconnect button pressed by {User}", component.User.Username);
        var disconnectPlayer = GetPlayer(component);
        if (disconnectPlayer == null)
        {
            await component.RespondAsync("Unable to process command", ephemeral: true);
            return;
        }

        playerHolder.RemovePlayer(component.GuildId!.Value);
        await disconnectPlayer.DisconnectAsync();
                
        await MessageHelper.EmbedButtonFollowupAsync(component, "I was disconnected");
    }

    private async Task SkipButtonHandler(SocketMessageComponent component)
    {
        await component.DeferAsync();
        logger.LogInformation("Skip button pressed by {User}", component.User.Username);
        var skipPlayer = GetPlayer(component);
        if (skipPlayer == null)
        {
            await component.RespondAsync("Unable to process command", ephemeral: true);
            return;
        }

        var songToBeSkipped = "Skipping currently playing song!";
        if (skipPlayer.CurrentSong != null)
            songToBeSkipped = $"Skipping {skipPlayer.CurrentSong.Title} - {skipPlayer.CurrentSong.Artist}!";
        await skipPlayer.Skip();

        await MessageHelper.EmbedButtonFollowupAsync(component, songToBeSkipped);
    }

    private Player? GetPlayer(SocketMessageComponent component)
    {
        var guildId = component.GuildId;
        if (guildId.HasValue)
            return playerHolder.GetExistingPlayer(guildId.Value);
        
        logger.LogError("Guild ID is null on SocketMessageComponent");
        return null;
    }
}
