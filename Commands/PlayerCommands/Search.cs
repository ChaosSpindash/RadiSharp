using DisCatSharp.Entities;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.Interactivity.Extensions;
using RadiSharp.Libraries.Managers;
using RadiSharp.Libraries.Utilities;
using RadiSharp.Typedefs;
using YTSearch.NET;

namespace RadiSharp.Commands;

public partial class PlayerCommands
{
            /// <summary>
        /// Starts a YouTube search and displays the results. The user can then choose one of the
        /// results to be added to the queue.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        /// <param name="query">The query to search for.</param>
        /// <remarks>
        /// This command requires the "Message Content" Privileged Intent to be enabled,
        /// as it needs to directly access user messages in order to queue a search result.
        /// Bots that are members to 100+ guilds require whitelisting by Discord Staff
        /// to continue using Privileged Intents.
        /// </remarks>
        [SlashCommand("search", "Search YouTube videos and optionally pick one to queue.")]
        public async Task SearchAsync(InteractionContext ctx,
            [Option("query", "The query to search for.")]
            string query)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var youtubeSearch = new YouTubeSearchClient();
            var search = await youtubeSearch.SearchYoutubeVideoAsync(query);

            List<string> videos = new();
            foreach (var i in search.Results)
            {
                videos.Add(i.Url!);
            }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.SearchEmbed(search)));
            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Search, [query, $"{search.Results.Count}"]);

            var interactivity = ctx.Client.GetInteractivity();
            var result = interactivity.WaitForMessageAsync(x => x.Author.Id == ctx.UserId);
            if (!result.Result.TimedOut)
            {
                if (Int32.TryParse(result.Result.Result.Content, out int index))
                {
                    if (index > 0 && index <= videos.Count)
                    {
                        _internalCall = true;
                        await PlayAsync(ctx, videos[index - 1]);
                    }
                    else
                    {
                        await ctx.EditResponseAsync(
                            new DiscordWebhookBuilder().AddEmbed(
                                EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrIndexOutOfRange)));
                    }
                }
                else
                {
                    await ctx.EditResponseAsync(
                        new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrInvalidArg)));
                }
            }
        }
}