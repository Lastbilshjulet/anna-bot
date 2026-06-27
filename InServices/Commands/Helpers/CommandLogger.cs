using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.Helpers;

public class CommandLogger<T>(ILogger<T> logger) : ICommandLogger<T>
{
    public void LogCommandCalled(SocketInteractionContext context, string option)
    {
        if (string.IsNullOrEmpty(option))
        {
            logger.LogInformation("{Command} called by {UserName}#{Discriminator} in guild {GuildName} ({GuildId}).", 
                typeof(T).Name, context.User.Username, context.User.Discriminator, context.Guild.Name, context.Guild.Id);
        }
        else
        {
            logger.LogInformation("{Command} - {Option} called by {UserName}#{Discriminator} in guild {GuildName} ({GuildId}).", 
                typeof(T).Name, option, context.User.Username, context.User.Discriminator, context.Guild.Name, context.Guild.Id);
        }
    }
}
