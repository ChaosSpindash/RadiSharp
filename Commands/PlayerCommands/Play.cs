using DisCatSharp.Entities;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.Lavalink;
using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Enums;
using RadiSharp.Libraries;
using RadiSharp.Libraries.Managers;
using RadiSharp.Libraries.Providers;
using RadiSharp.Libraries.Utilities;
using RadiSharp.Typedefs;
using YTSearch.NET;

namespace RadiSharp.Commands;

public partial class PlayerCommands
{
            /// <summary>
        /// Plays a track from a URL or search query.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        /// <param name="query">The query to search for.</param>
        [SlashCommand("play", "Play a track from a URL or search query.")]
        public async Task PlayAsync(InteractionContext ctx, [Autocomplete(typeof(SearchAutocompleteProvider))] [Option("query", "The query to search for.", true)] string? query)
        {
            if (!_internalCall)
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            if (ctx.Member?.VoiceState?.Channel is null)
            {
                if (!_internalCall)
                {
                    await ctx.EditResponseAsync(
                        new DiscordWebhookBuilder()
                            .AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                }
                else
                {
                    await new DiscordMessageBuilder()
                        .AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice))
                        .SendAsync(ctx.Channel);
                }

                return;
            }


            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                var session = lavalink.ConnectedSessions.Values.First();
                if (ctx.Member.VoiceState.Channel.Type != ChannelType.Voice &&
                    ctx.Member.VoiceState.Channel.Type != ChannelType.Stage)
                {
                    if (!_internalCall)
                    {
                        await ctx.EditResponseAsync(
                            new DiscordWebhookBuilder().AddEmbed(
                                EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrVoiceChannelInvalid)));
                    }
                    else
                    {
                        await new DiscordMessageBuilder()
                            .AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrVoiceChannelInvalid))
                            .SendAsync(ctx.Channel);
                    }

                    return;
                }

                await session.ConnectAsync(ctx.Member.VoiceState.Channel);
                EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Connect, [$"{ctx.Member.VoiceState.Channel.Id}"]);
                guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

                _trackStartedHandler = CreateTrackStartedHandler(ctx);
                guildPlayer!.TrackStarted += _trackStartedHandler;

                _trackEndedHandler = CreateTrackEndedHandler(ctx);
                guildPlayer.TrackEnded += _trackEndedHandler;
            }

            string? originalQuery = query;

            LavalinkSearchType searchType;

            // Check if the query is a URL
            if (Uri.TryCreate(query, UriKind.Absolute, out _))
            {
                searchType = LavalinkSearchType.Plain;
            }
            // Check if the query is a YouTube URL
            else if (query!.Contains("youtube.com") || query.Contains("youtu.be"))
            {
                // Check if the query is a YouTube Playlist URL
                searchType = query.Contains("/playlist?list=") ? LavalinkSearchType.Plain : LavalinkSearchType.Youtube;
            }
            // Check if the query is a Spotify URL
            else if (query.Contains("spotify.com"))
            {
                searchType = LavalinkSearchType.Spotify;
            }
            else
            {
                // Prepare YouTube search client
                var youtubeClient = new YouTubeSearchClient();
                var search = await youtubeClient.SearchYoutubeVideoAsync(query);
                if (search.Results.Count > 0)
                {
                    query = search.Results.First().Url;
                }

                searchType = LavalinkSearchType.Plain;
            }

            if (query != null)
            {
                var loadResult = await guildPlayer.LoadTracksAsync(searchType, query);

                if (loadResult.LoadType is LavalinkLoadResultType.Empty or LavalinkLoadResultType.Error)
                {
                    if (!_internalCall)
                    {
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                            .AddEmbed(EmbedGenerator.NoMatchErrorEmbed(originalQuery)));
                    }
                    else
                    {
                        await new DiscordMessageBuilder()
                            .AddEmbed(EmbedGenerator.NoMatchErrorEmbed(originalQuery))
                            .SendAsync(ctx.Channel);
                    }
                    EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Error, ["ERR_SEARCH_QUERY_NO_MATCH", $"No matches found for query: {query}"]);
                    return;
                }

                if (loadResult.LoadType == LavalinkLoadResultType.Playlist)
                {
                    LavalinkPlaylist playlist = loadResult.GetResultAs<LavalinkPlaylist>();

                    _queueManager.AddPlaylist(new RadiPlaylist(playlist, ctx.Member));

                    if (!_internalCall)
                    {
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                            .AddEmbed(EmbedGenerator.AddPlaylistEmbed(playlist, ctx.Member.Mention)));
                    }
                    else
                    {
                        await new DiscordMessageBuilder()
                            .AddEmbed(EmbedGenerator.AddPlaylistEmbed(playlist, ctx.Member.Mention))
                            .SendAsync(ctx.Channel);
                    }
                    EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.QueuePlaylist, [$"{query} - {playlist.Info.Name} ({playlist.Tracks.Count} tracks)"]);
                }
                else
                {
                    LavalinkTrack track;
                    switch (loadResult.LoadType)
                    {
                        case LavalinkLoadResultType.Track:
                            track = loadResult.GetResultAs<LavalinkTrack>();
                            break;
                        case LavalinkLoadResultType.Search:
                            track = loadResult.GetResultAs<List<LavalinkTrack>>().First();
                            break;
                        default:
                            if (!_internalCall)
                            {
                                await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                                    .AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkLoadFailed)));
                            }
                            else
                            {
                                await new DiscordMessageBuilder()
                                    .AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkLoadFailed))
                                    .SendAsync(ctx.Channel);
                            }
                            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Error, ["ERR_LAVALINK_INVALID_LOAD_RESULT_TYPE", $"Invalid load result type for query: {query}"]);
                            return;
                    }

                    RadiTrack radiTrack = new(track, ctx.Member);
                    _queueManager.Add(radiTrack);

                    if (!_internalCall)
                    {
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                            .AddEmbed(EmbedGenerator.AddTrackEmbed(radiTrack)));
                    }
                    else
                    {
                        await new DiscordMessageBuilder()
                            .AddEmbed(EmbedGenerator.AddTrackEmbed(radiTrack))
                            .SendAsync(ctx.Channel);
                    }
                    EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Queue, [$"{query} - {track.Info.Title}"]);
                }
            }


            if (!_queueManager.IsPlaying)
            {
                _queueManager.IsPlaying = true;
                await guildPlayer.PlayAsync(_queueManager.Next()!.Track);
            }
        }

}