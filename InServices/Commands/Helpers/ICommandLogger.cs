using System.Text;
using Discord.Interactions;

namespace anna_bot.InServices.Commands.Helpers;

public interface ICommandLogger<T>
{
    void LogCommandCalled(SocketInteractionContext context, string query = "");
}
