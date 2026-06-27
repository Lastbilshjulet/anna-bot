using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace anna_bot.InServices.Commands.Helpers;

public class MessageHelper
{
    public static async Task EmbedFollowup(SocketInteractionContext context, string message, bool ephemeral)
    {
        var user = context.User;
        var embed = new EmbedBuilder()
            .WithColor(0x0600ff)
            .WithTitle(message)
            .WithTimestamp(DateTime.Now)
            .WithFooter(x => x.WithText($"By {user.GlobalName}").WithIconUrl(user.GetDisplayAvatarUrl()));
        
        await context.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: ephemeral);
    }
}
