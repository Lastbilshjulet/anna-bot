using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using anna_bot.Domain.Models;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;

namespace anna_bot.InServices.Commands.Helpers;

public class MessageHelper
{
    public static async Task EmbedFollowupAsync(SocketInteractionContext context, string message, bool ephemeral)
    {
        var user = context.User as SocketGuildUser;
        var embed = EmbedBuilder(message, user!);
        
        var messageSent = await context.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: ephemeral, flags: MessageFlags.SuppressNotification);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            await messageSent.DeleteAsync();
        });
    }

    public static async Task EmbedButtonFollowupAsync(SocketMessageComponent component, string message)
    {
        var user = component.User as SocketGuildUser;
        var embed = EmbedBuilder(message, user!);
        
        var messageSent = await component.FollowupAsync(embed: embed.Build(), flags: MessageFlags.SuppressNotification);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            await messageSent.DeleteAsync();
        });
    }

    public static async Task EmbedFollowupAsync(SocketInteractionContext context, string title, Song? currentSong, List<Song> queuedSongs)
    {
        var user = context.User as SocketGuildUser;
        var embed = EmbedBuilder(title, user!);

        if (currentSong != null)
            embed.AddField($"Currently playing: {currentSong.Title} - {currentSong.Artist}", $"{currentSong.FormattedDuration()} - Requested by {GetUsername(context.Guild, currentSong)} | [Source]({currentSong.GetYouTubeUrl()})");
        
        foreach (var (song, i) in queuedSongs.Select((song, i) => (song, i)))
        {
            embed.AddField(
                $"{i + 1}. {song.Title} - {song.Artist}", 
                $"{song.FormattedDuration()} - Requested by {GetUsername(context.Guild, song)} | [Source]({song.GetYouTubeUrl()})");
            
            if (i == 9)
                break;
        }
        
        var messageSent = await context.Interaction.FollowupAsync(embed: embed.Build(), flags: MessageFlags.SuppressNotification);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(10));
            await messageSent.DeleteAsync();
        });
    }

    private static EmbedBuilder EmbedBuilder(string message, SocketGuildUser user)
    {
        return new EmbedBuilder()
            .WithColor(0x0600ff)
            .WithTitle(message)
            .WithTimestamp(DateTime.Now)
            .WithFooter(x => x.WithText($"By {user.DisplayName}").WithIconUrl(user.GetDisplayAvatarUrl()));
    }

    public static async Task EmbedSendMessageAsync(SocketTextChannel textChannel, string title)
    {
        var embed = new EmbedBuilder()
            .WithColor(0x0600ff)
            .WithTitle(title)
            .WithTimestamp(DateTime.Now);
        
        var messageSent = await textChannel.SendMessageAsync(embed: embed.Build(), flags: MessageFlags.SuppressNotification);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            await messageSent.DeleteAsync();
        });
    }

    public static async Task<RestUserMessage?> EmbedSendMessageAsync(
        Player player, 
        SocketTextChannel textChannel,
        Song song)
    {
        var components = SongComponentBuilder(player, textChannel, song);

        var messageSent = await textChannel.SendMessageAsync(components: components.Build(), flags: MessageFlags.SuppressNotification);
        _ = Task.Run(async () =>
        {
            await Task.Delay(song.Duration);
            await messageSent.DeleteAsync();
        });

        return messageSent;
    }

    public static async Task EmbedSendMessageAsync(SocketMessageComponent component, Player player)
    {
        var currentSong = player.CurrentSong!;
        var components = SongComponentBuilder(player, player.TextChannel!, currentSong);

        await component.UpdateAsync(msg =>
        {
            msg.Components = components.Build();
            msg.Flags = MessageFlags.SuppressNotification | MessageFlags.ComponentsV2;
        });
    }

    private static ComponentBuilderV2 SongComponentBuilder(
        Player player, 
        SocketTextChannel textChannel, 
        Song song)
    {
        var backButtonBuilder = new ButtonBuilder("Back", "BackButton")
            .WithEmote(new Emoji("⏮️"))
            .WithStyle(ButtonStyle.Danger);
        ButtonBuilder pauseButtonBuilder;
        if (player.IsPlaying)
        {
            pauseButtonBuilder = new ButtonBuilder("Pause", "PauseButton")
                .WithEmote(new Emoji("⏸️"))
                .WithStyle(ButtonStyle.Success);
        }
        else
        {
            pauseButtonBuilder = new ButtonBuilder("Play", "PlayButton")
                .WithEmote(new Emoji("▶️"))
                .WithStyle(ButtonStyle.Success);
        }
        var skipButtonBuilder = new ButtonBuilder("Skip", "SkipButton")
            .WithEmote(new Emoji("⏭️"))
            .WithStyle(ButtonStyle.Danger);
        var repeatButtonBuilder = new ButtonBuilder("Repeat", "RepeatButton")
            .WithEmote(new Emoji("🔂"))
            .WithStyle(ButtonStyle.Success);
        var volumeDownButtonBuilder = new ButtonBuilder("Down", "VolumeDownButton")
            .WithEmote(new Emoji("🔉"))
            .WithStyle(ButtonStyle.Secondary);
        var volumeUpButtonBuilder = new ButtonBuilder("Up", "VolumeUpButton")
            .WithEmote(new Emoji("🔊"))
            .WithStyle(ButtonStyle.Primary);
        var disconnectButtonBuilder = new ButtonBuilder("Disconnect", "DisconnectButton")
            .WithEmote(new Emoji("🔌"))
            .WithStyle(ButtonStyle.Danger);
        
        var title = song.IsAutoPlayed ? "Auto-Playing..." : "Now Playing...";
        var components = new ComponentBuilderV2()
            .WithContainer(x => x
                .WithAccentColor(0x0600ff)
                .WithTextDisplay($"## {title}{(player.Repeat ? " - (🔂)" : "")}{(player.Volume != 0.1f ? $" - (🔊{player.DisplayVolume})" : "")}")
                .WithTextDisplay($"### :notes: [{song.Title} - {song.Artist}]({song.GetYouTubeUrl()}) {song.FormattedDuration()}")
                .WithTextDisplay($"Requested by: {GetUsername(textChannel.Guild, song)}")
                .WithSeparator( separator => separator
                    .WithIsDivider(true)
                    .WithSpacing(SeparatorSpacingSize.Small))
                .WithActionRow([backButtonBuilder, pauseButtonBuilder, repeatButtonBuilder, skipButtonBuilder])
                .WithActionRow([volumeDownButtonBuilder, volumeUpButtonBuilder, disconnectButtonBuilder]));
        return components;
    }

    private static string GetUsername(SocketGuild guild, Song song)
    {
        if (song.RequestedByUserId == 0)
            return song.RequestedBy;
        return guild.GetUser(song.RequestedByUserId)?.DisplayName ?? song.RequestedBy;
    }
}
