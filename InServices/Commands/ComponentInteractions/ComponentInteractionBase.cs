using System;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.Domain.Models;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.ComponentInteractions;

public abstract class ComponentInteractionBase(PlayerState playerState, ILogger logger) : InteractionModuleBase<SocketInteractionContext>
{
    protected async Task HandleAsync(Func<Player, Task> handler)
    {
        try
        {
            var interactionName = Context.Interaction is SocketMessageComponent component
                ? component.Data.CustomId
                : Context.Interaction.Type.ToString();
            logger.LogInformation("{Interaction} pressed by {User}", interactionName, Context.User.Username);
            var player = await ValidationHelper.ValidateAndGetPlayer(Context, logger, playerState);
            if (player != null)
                await handler(player);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred during component interaction handling.");
        }
    }
}
