using System;
using System.Threading.Tasks;
using anna_bot.Domain.Models.Configurations;
using anna_bot.InServices.Commands.Helpers;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace anna_bot.InServices.Commands;

public class Album(
    IOptions<MusicConfiguration> musicConfig,
    ILogger<Album> logger, 
    ICommandLogger<Album> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("album", "Returns album generator link.")]
    public async Task AlbumAsync()
    {
        try
        {
            await DeferAsync();
            commandLogger.LogCommandCalled(Context);
        
            await MessageHelper.EmbedFollowupAsync(Context, $"{musicConfig.Value.AlbumGenerator}", false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
