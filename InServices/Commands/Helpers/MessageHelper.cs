using System;
using System.Threading.Tasks;
using anna_bot.Domain.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace anna_bot.InServices.Commands.Helpers;

public class MessageHelper
{
    public static async Task EmbedFollowupAsync(SocketInteractionContext context, string message, bool ephemeral)
    {
        var user = context.User;
        var embed = new EmbedBuilder()
            .WithColor(0x0600ff)
            .WithTitle(message)
            .WithTimestamp(DateTime.Now)
            .WithFooter(x => x.WithText($"By {user.GlobalName}").WithIconUrl(user.GetDisplayAvatarUrl()));
        
        await context.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: ephemeral, flags: MessageFlags.SuppressNotification);
    }

    public static async Task EmbedSendMessageAsync(SocketTextChannel textChannel, string title, Song? song)
    {
        var embed = new EmbedBuilder()
            .WithColor(0x0600ff)
            .WithTitle(title)
            .WithTimestamp(DateTime.Now);

        if (song != null)
        {
            embed.Description = $":notes: [{song.Title} - {song.Artist}]({song.Source}) {song.Duration}";
            embed.Footer = new EmbedFooterBuilder().WithText($"By {song.RequestedBy}");
        }
        
        await textChannel.SendMessageAsync(embed: embed.Build(), flags: MessageFlags.SuppressNotification);
    }
}
