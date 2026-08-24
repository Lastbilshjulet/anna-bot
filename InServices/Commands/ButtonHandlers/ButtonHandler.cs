using System;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.Domain.Models;
using anna_bot.InServices.Commands.Helpers;
using anna_bot.OutServices.UseCases;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ButtonHandlers;

public class ButtonHandler(ISongDbService songDbService, PlayerState playerState, ILogger<ButtonHandler> logger)
{
    public async Task OnButtonExecuted(SocketMessageComponent component)
    {
        try
        {
            logger.LogInformation("{Button} pressed by {User}", component.Data.CustomId, component.User.Username);
            var player = await GetPlayer(component);
            if (player == null)
                return;
            
            switch (component.Data.CustomId)
            {
                case "BackButton":
                    await BackButtonHandler(component, player);
                    break;
                case "PlayButton":
                    await PlayButtonHandler(component, player);
                    break;
                case "PauseButton":
                    await PauseButtonHandler(component, player);
                    break;
                case "SkipButton":
                    await SkipButtonHandler(component, player);
                    break;
                case "RepeatButton":
                    await RepeatButtonHandler(component, player);
                    break;
                case "VolumeDownButton":
                    await VolumeDownButtonHandler(component, player);
                    break;
                case "VolumeUpButton":
                    await VolumeUpButtonHandler(component, player);
                    break;
                case "DisconnectButton":
                    await DisconnectButtonHandler(component, player);
                    break;
                case "ToggleAutoplayButton":
                    await ToggleAutoPlayButtonHandler(component, player);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred during button handling.");
        }
    }

    private async Task ToggleAutoPlayButtonHandler(SocketMessageComponent component, Player player)
    {
        if (player.CurrentSong == null)
        {
            await MessageHelper.EmbedButtonFollowupAsync(component, "Song is null, can't toggle auto play");
            return;
        }
        
        songDbService.ToggleAutoplay(player.CurrentSong);
        await MessageHelper.EmbedButtonFollowupAsync(component, $"Toggled autoplay to {player.CurrentSong.Autoplay} for  {player.CurrentSong.Title}!");
    }

    private static async Task BackButtonHandler(SocketMessageComponent component, Player player)
    {
        await component.DeferAsync();
        await player.PlayPreviousSong();
        
        await MessageHelper.EmbedButtonFollowupAsync(component, "Going back to previous song!");
    }

    private static async Task PlayButtonHandler(SocketMessageComponent component, Player player)
    {
        if (!player.IsPlaying)
            player.Pause();

        await MessageHelper.EmbedSendMessageAsync(component, player);
    }

    private static async Task PauseButtonHandler(SocketMessageComponent component, Player player)
    {
        if (player.IsPlaying)
            player.Pause();

        await MessageHelper.EmbedSendMessageAsync(component, player);
    }

    private static async Task SkipButtonHandler(SocketMessageComponent component, Player player)
    {
        await component.DeferAsync();
        var songToBeSkipped = "Skipping currently playing song!";
        if (player.CurrentSong != null)
            songToBeSkipped = $"Skipping {player.CurrentSong.Title} - {player.CurrentSong.Artist}!";
        await player.Skip();

        await MessageHelper.EmbedButtonFollowupAsync(component, songToBeSkipped);
    }

    private static async Task RepeatButtonHandler(SocketMessageComponent component, Player player)
    {
        player.ToggleRepeat();

        await MessageHelper.EmbedSendMessageAsync(component, player);
    }

    private static async Task VolumeDownButtonHandler(SocketMessageComponent component, Player player)
    {
        player.DecreaseVolume();

        await MessageHelper.EmbedSendMessageAsync(component, player);
    }

    private static async Task VolumeUpButtonHandler(SocketMessageComponent component, Player player)
    {
        player.IncreaseVolume();

        await MessageHelper.EmbedSendMessageAsync(component, player);
    }

    private static async Task DisconnectButtonHandler(SocketMessageComponent component, Player player)
    {
        await component.DeferAsync();
        await player.DisconnectAsync();
                
        await MessageHelper.EmbedButtonFollowupAsync(component, "I was disconnected");
    }

    private async Task<Player?> GetPlayer(SocketMessageComponent component)
    {
        var guildUser = component.User as SocketGuildUser;
        var guildId = component.GuildId;
        if (guildId.HasValue)
        {
            var player = playerState.GetExistingPlayer(guildId.Value);

            if (player == null)
            {
                logger.LogError("Player not found on button click, should not be possible");
                await MessageHelper.EmbedButtonFollowupAsync(component, "Unable to process command. ");
                return null;
            }

            if (player.VoiceChannel != guildUser?.VoiceChannel)
            {
                await MessageHelper.EmbedButtonFollowupAsync(component, "You need to be connected to the same voice channel as me in order to use buttons.");
                return null;
            }

            return player;
        }
        
        logger.LogError("Guild ID is null on SocketMessageComponent");
        await MessageHelper.EmbedButtonFollowupAsync(component, "Unable to process command. ");
        return null;
    }
}
