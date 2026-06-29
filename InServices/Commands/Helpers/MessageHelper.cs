using System;
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
        var user = context.User;
        var embed = EmbedBuilder(message, user);
        
        var messageSent = await context.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: ephemeral, flags: MessageFlags.SuppressNotification);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            await messageSent.DeleteAsync();
        });
    }

    public static async Task EmbedButtonFollowupAsync(SocketMessageComponent component, string message)
    {
        var user = component.User;
        var embed = EmbedBuilder(message, user);
        
        var messageSent = await component.FollowupAsync(embed: embed.Build(), flags: MessageFlags.SuppressNotification);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            await messageSent.DeleteAsync();
        });
    }

    private static EmbedBuilder EmbedBuilder(string message, SocketUser user)
    {
        return new EmbedBuilder()
            .WithColor(0x0600ff)
            .WithTitle(message)
            .WithTimestamp(DateTime.Now)
            .WithFooter(x => x.WithText($"By {user.GlobalName}").WithIconUrl(user.GetDisplayAvatarUrl()));
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

    public static async Task<RestUserMessage?> EmbedSendMessageAsync(SocketTextChannel textChannel, string title, Song song)
    {
        // TODO: previous song
        // TODO: repeat/loop
        // TODO: Add emotes to buttons
        var skipButtonBuilder = new ButtonBuilder("Skip", "SkipButton")
            .WithStyle(ButtonStyle.Success);
        //var pauseButtonBuilder = new ButtonBuilder("Pause", "PauseButton")
        //    .WithStyle(ButtonStyle.Primary);
        var disconnectButtonBuilder = new ButtonBuilder("Disconnect", "DisconnectButton")
            .WithStyle(ButtonStyle.Danger);

        var components = new ComponentBuilderV2()
            .WithContainer(x => x
                .WithAccentColor(0x0600ff)
                .WithTextDisplay($"## {title}")
                .WithTextDisplay($"### :notes: [{song.Title} - {song.Artist}]({song.Source}) {song.FormattedDuration()}")
                .WithSeparator( separator => separator
                    .WithIsDivider(true)
                    .WithSpacing(SeparatorSpacingSize.Small))
                .WithActionRow([skipButtonBuilder, disconnectButtonBuilder]));
        
        var messageSent = await textChannel.SendMessageAsync(components: components.Build(), flags: MessageFlags.SuppressNotification);
        _ = Task.Run(async () =>
        {
            await Task.Delay(song.Duration);
            await messageSent.DeleteAsync();
        });

        return messageSent;
    }
}
