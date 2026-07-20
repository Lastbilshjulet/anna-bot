using System;
using System.Threading.Tasks;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class Restart(
    ILogger<Restart> logger, 
    ICommandLogger<Restart> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("restart", "Restarts the bot. Useful if audio is stuck.")]
    public async Task RestartAsync()
    {
        try
        {
            await DeferAsync();
            commandLogger.LogCommandCalled(Context);
        
            await MessageHelper.EmbedFollowupAsync(Context, "Restarting the bot!", false);
            
            await Task.Delay(1000);
            
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
