using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using YTSearch.NET;

namespace RadiSharp.Libraries.Providers;

/// <summary>
/// Provides search autocomplete options.
/// </summary>
public class SearchAutocompleteProvider : IAutocompleteProvider
{
    /// <summary>
    /// The provider method.
    /// </summary>
    /// <param name="ctx">The autocomplete context.</param>
    /// <returns>A list of search results for autocompletion.</returns>
    public async Task<IEnumerable<DiscordApplicationCommandAutocompleteChoice>> Provider(AutocompleteContext ctx)
    {
        // Get the query and cast it to a string
        var query = (string)ctx.FocusedOption.Value;
        // If the query is less than 3 characters, return an empty list
        if (query.Length < 3)
        {
            return await Task.FromResult(new List<DiscordApplicationCommandAutocompleteChoice>());
        }
        
        var youtubeSearch = new YouTubeSearchClient();
        var search = await youtubeSearch.SearchYoutubeVideoAsync(query);
        
        var options = new List<DiscordApplicationCommandAutocompleteChoice>();
        
        foreach (var result in search.Results)
        {
            if (result is { Title: not null, Url: not null })
                options.Add(new DiscordApplicationCommandAutocompleteChoice(result.Title, result.Url));
        }
        return await Task.FromResult(options.AsEnumerable());
    }
}