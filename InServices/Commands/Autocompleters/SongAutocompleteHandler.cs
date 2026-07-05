using System;
using System.Linq;
using System.Threading.Tasks;
using anna_bot.Domain;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace anna_bot.InServices.Commands.Autocompleters;

public class SongAutocompleteHandler(PlayerState playerState, ILogger<SongAutocompleteHandler> logger) : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, 
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, 
        IServiceProvider services)
    {
        try
        {
            if (parameter.Name != "query")
                return Task.FromResult(AutocompletionResult.FromError(InteractionCommandError.Unsuccessful, "Parameter not found"));
        
            var userInput = autocompleteInteraction.Data.Current.Value as string ?? string.Empty;

            logger.LogInformation("Autocompleting songs from {Query}", userInput);

            var songs = playerState.GetAllAvailableSongs();
            if (userInput != string.Empty)
                songs = songs.Where(x => x.Title.Contains(userInput, StringComparison.OrdinalIgnoreCase) || x.Artist.Contains(userInput, StringComparison.OrdinalIgnoreCase)).ToList();
        
            songs = songs.OrderBy(x => x.TimesPlayed * -1).ToList();
            
            return Task.FromResult(AutocompletionResult.FromSuccess(songs.Select(x => 
            {
                var durationPart = $" | {x.FormattedDuration()}";
                var maxTitleArtistLength = 100 - durationPart.Length;
                    
                var titleAndArtist = $"{x.Title} - {x.Artist}";
                if (titleAndArtist.Length > maxTitleArtistLength)
                    titleAndArtist = titleAndArtist[..maxTitleArtistLength];
                    
                var fullDisplay = $"{titleAndArtist}{durationPart}";
                    
                return new AutocompleteResult(fullDisplay, x.YoutubeId);
            }).Take(25)));
        }
        catch (Exception exception)
        {
            return Task.FromException<AutocompletionResult>(exception);
        }
    }
}
