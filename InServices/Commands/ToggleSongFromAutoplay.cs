using System;
using System.Linq;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.InServices.Commands.Autocompleters;
using anna_bot.InServices.Commands.Helpers;
using anna_bot.OutServices.UseCases;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands;

public class ToggleSongFromAutoplay(
    PlayerState playerState, 
    ISongDbService songDbService, 
    ILogger<ToggleSongFromAutoplay> logger, 
    ICommandLogger<ToggleSongFromAutoplay> commandLogger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("togglesongfromautoplay", "Toggles song from autoplay!")]
    public async Task ToggleSongFromAutoplayAsync([Autocomplete(typeof(SongAutocompleteHandler))] string query)
    {
        try
        {
            await DeferAsync(ephemeral: true);
            commandLogger.LogCommandCalled(Context, query);
            
            var selectedSong = playerState.GetAllAvailableSongs().FirstOrDefault(x => x.YoutubeId == query);
            if (selectedSong == null)
            {
                await MessageHelper.EmbedFollowupAsync(Context, "Song could not be found to update.", true);
                return;
            }
            
            // Won't update in cached song lists in players
            logger.LogInformation("Toggling autoplay for {SelectedSongTitle}", selectedSong.Title);
            selectedSong.Autoplay = !selectedSong.Autoplay;
            var updatedSong = songDbService.ToggleAutoplay(selectedSong);
        
            await MessageHelper.EmbedFollowupAsync(Context, $"Toggled autoplay for {updatedSong.Title} to {updatedSong.Autoplay}", true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {CommandName}", GetType().Name);
            await MessageHelper.EmbedFollowupAsync(Context, $"Failed to process {GetType().Name}", true);
        }
    }
}
