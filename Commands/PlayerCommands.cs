using DisCatSharp.Entities;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.Common.Utilities;
using DisCatSharp.Interactivity.Extensions;
using DisCatSharp.Lavalink;
using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Enums;
using DisCatSharp.Lavalink.EventArgs;
using RadiSharp.Libraries;
using RadiSharp.Libraries.Managers;
using RadiSharp.Libraries.Providers;
using RadiSharp.Libraries.Utilities;
using RadiSharp.Typedefs;
using YTSearch.NET;

namespace RadiSharp.Commands
{
    // ReSharper disable once ClassNeverInstantiated.Global
    /// <summary>
    /// Contains commands for controlling the player and other related functions.
    /// </summary>
    public class PlayerCommands : ApplicationCommandsModule
    {
        /// <summary>
        /// The queue manager instance.
        /// </summary>
        private readonly QueueManager _queueManager = QueueManager.Instance;

        private AsyncEventHandler<LavalinkGuildPlayer, LavalinkTrackStartedEventArgs>? _trackStartedHandler;
        private AsyncEventHandler<LavalinkGuildPlayer, LavalinkTrackEndedEventArgs>? _trackEndedHandler;

        /// <summary>
        /// Determines whether the command was called internally or by a user.
        /// </summary>
        private bool _internalCall;

        /// <summary>
        /// Stops playback, clears the queue and leaves the voice channel.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        [SlashCommand("stop", "Stop playback, clear queue and leave the voice channel.")]
        // ReSharper disable once MemberCanBePrivate.Global
        public async Task StopAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }
            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);
            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Error, ["ERR_LAVALINK_NO_SESSION", "No Lavalink session found for guild"]);
                return;
            }

            _queueManager.Clear();
            await guildPlayer.DisconnectAsync();
            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Disconnect);
            if (_trackStartedHandler != null) guildPlayer.TrackStarted -= _trackStartedHandler;
            if (_trackEndedHandler != null) guildPlayer.TrackEnded -= _trackEndedHandler;
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.StatusEmbed(EmbedStatusType.StatusDisconnect)));
            await Task.Delay(10000);
            await ctx.DeleteResponseAsync();
        }

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

        // ReSharper disable once UnusedMember.Local
        /// <summary>
        /// Disconnects the bot if it is left alone in a voice channel for a certain amount of time.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        /// <param name="guildPlayer">The player to disconnect.</param>
        private async Task DisconnectIdlePlayer(InteractionContext ctx, LavalinkGuildPlayer guildPlayer)
        {
            _queueManager.Clear();
            await guildPlayer.DisconnectAsync();
            if (_trackStartedHandler != null) guildPlayer.TrackStarted -= _trackStartedHandler;
            if (_trackEndedHandler != null) guildPlayer.TrackEnded -= _trackEndedHandler;
            var msg = await new DiscordMessageBuilder()
                .AddEmbed(EmbedGenerator.StatusEmbed(EmbedStatusType.StatusInactivity)).SendAsync(ctx.Channel);
            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Disconnect);
            await Task.Delay(10000);
            await msg.DeleteAsync();
        }

        /// <summary>
        /// Pauses the current track.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        [SlashCommand("pause", "Pause the current track.")]
        public async Task PauseAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            if (guildPlayer.Player.Paused)
            {
                await ctx.DeleteResponseAsync();
                return;
            }

            await guildPlayer.PauseAsync();
            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Pause);
            await ctx.DeleteResponseAsync();
        }

        /// <summary>
        /// Resumes the current track.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        [SlashCommand("resume", "Resume the current track.")]
        public async Task ResumeAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            if (!guildPlayer.Player.Paused)
            {
                await ctx.DeleteResponseAsync();
                return;
            }

            await guildPlayer.ResumeAsync();
            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Resume);
            await ctx.DeleteResponseAsync();
        }

        /// <summary>
        /// Displays the current queue.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        [SlashCommand("queue", "Display the current queue.")]
        public async Task QueueAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (_queueManager.PlaylistCount() == 0)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .AddEmbed(EmbedGenerator.QueueEmbed(_queueManager))
                .AddComponents(EmbedGenerator.QueueComponents(_queueManager)));
        }

        /// <summary>
        /// Skips the current track.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        [SlashCommand("skip", "Skip the current track.")]
        public async Task SkipAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            await guildPlayer.PlayAsync(_queueManager.Next(true)!.Track);
            await ctx.DeleteResponseAsync();
        }

        /// <summary>
        /// Clears the current queue.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        [SlashCommand("clear", "Clear the current queue.")]
        public async Task ClearAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            _queueManager.Clear();
            await guildPlayer.StopAsync();
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.StatusEmbed(EmbedStatusType.StatusClearQueue)));
            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Clear);
            await Task.Delay(10000);
            await ctx.DeleteResponseAsync();
        }

        /// <summary>
        /// Plays the previous track.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        [SlashCommand("prev", "Play the previous track.")]
        public async Task PreviousAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            await guildPlayer.PlayAsync(_queueManager.Previous()!.Track);
            await ctx.DeleteResponseAsync();
        }

        /// <summary>
        /// Loops the current track.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        [SlashCommand("loop", "Loop the current track.")]
        public async Task LoopAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            _queueManager.ToggleLoop();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"🔂 Loop {(_queueManager.Loop ? "Enabled" : "Disabled")}")
                .WithColor(DiscordColor.Green)));
            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Loop, [_queueManager.Loop ? "Enabled" : "Disabled"]);
        }

        /// <summary>
        /// Shuffles the current queue.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        [SlashCommand("shuffle", "Shuffle the current queue.")]
        public async Task ShuffleAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            _queueManager.ToggleShuffle();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"🔀 Shuffle {(_queueManager.Shuffle ? "Enabled" : "Disabled")}")
                .WithColor(DiscordColor.Green)));
            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Shuffle, [_queueManager.Shuffle ? "Enabled" : "Disabled"]);
        }

        /// <summary>
        /// Loops the current queue.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        [SlashCommand("loopq", "Loop the current queue.")]
        public async Task LoopQueueAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            _queueManager.ToggleLoopQueue();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"🔁 Loop Queue {(_queueManager.LoopQueue ? "Enabled" : "Disabled")}")
                .WithColor(DiscordColor.Green)));
            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.LoopQueue, [_queueManager.LoopQueue ? "Enabled" : "Disabled"]);
        }

        /// <summary>
        /// Removes a track from the queue.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        /// <param name="index">The index of the track to remove.</param>
        [SlashCommand("remove", "Remove a track from the queue.")]
        public async Task RemoveAsync(InteractionContext ctx,
            [Option("index", "The index of the track to remove.")]
            int index)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            if (index < 1 || index > _queueManager.PlaylistCount())
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrIndexOutOfRange)));
                return;
            }

            var track = _queueManager.GetTrack(index);
            _queueManager.Remove(index);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .AddEmbed(EmbedGenerator.RemoveTrackEmbed(track!)));
            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Remove, [track!.Track.Info.Title]);
        }

        /// <summary>
        /// Moves a track in the queue.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        /// <param name="from">The index of the track to move.</param>
        /// <param name="to">The index to move the track to.</param>
        [SlashCommand("move", "Move a track in the queue.")]
        public async Task MoveAsync(InteractionContext ctx,
            [Option("from", "The index of the track to move.")]
            int from,
            [Option("to", "The index to move the track to.")]
            int to)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            if (from < 1 || from > _queueManager.PlaylistCount() || to < 1 || to > _queueManager.PlaylistCount())
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrIndexOutOfRange)));
                return;
            }

            _queueManager.Move(from, to);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle("📝 Moved in Queue")
                .WithDescription($"Moved track from index {from} to index {to}.")
                .WithColor(DiscordColor.Green)));
            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Move, [$"{from}", $"{to}"]);
        }

        /// <summary>
        /// Skips to a specific track in the queue.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        /// <param name="index">The index of the track to skip to.</param>
        [SlashCommand("skipto", "Skip to a specific track in the queue.")]
        public async Task SkipToAsync(InteractionContext ctx,
            [Option("index", "The index of the track to skip to.")]
            int index)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrQueueEmpty)));
                return;
            }

            if (index < 1 || index > _queueManager.PlaylistCount())
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrIndexOutOfRange)));
            }

            await guildPlayer.PlayAsync(_queueManager.SkipTo(index)!.Track);
            await ctx.DeleteResponseAsync();
        }

        /// <summary>
        /// Disconnects from the voice channel.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        /// <remarks>
        /// Many music bots have this as a (sometimes undocumented) alias for their Stop/Leave commands.
        /// Unfortunately, the introduction of slash commands to Discord made the use of traditional prefixed commands
        /// more and more obsolete, so this command can no longer be easily hidden from plain sight.
        /// It is still being kept here as a little easter egg.
        /// </remarks>
        [SlashCommand("fuckoff", "Leave the voice channel.")]
        public async Task LeaveAsync(InteractionContext ctx)
        {
            await StopAsync(ctx);
        }

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

        #region Event Handlers

        /// <summary>
        /// Event handler wrapper for when playback has started.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        /// <returns>The event handler for the LavalinkTrackStarted event.</returns>
        private AsyncEventHandler<LavalinkGuildPlayer, LavalinkTrackStartedEventArgs> CreateTrackStartedHandler(
            InteractionContext ctx)
        {
            return async (_, e) =>
            {
                await new DiscordMessageBuilder()
                    .AddEmbed(EmbedGenerator.PlayerEmbed(_queueManager))
                    .AddComponents(EmbedGenerator.PlayerComponents(_queueManager))
                    .SendAsync(ctx.Channel);
                EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Play, [$"{e.Track.Info.Title} ({e.Track.Info.Uri})"]);
            };
        }

        /// <summary>
        /// Event handler wrapper for when playback has ended.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        /// <returns>The event handler for the LavalinkTrackEnded event.</returns>
        private AsyncEventHandler<LavalinkGuildPlayer, LavalinkTrackEndedEventArgs> CreateTrackEndedHandler(
            InteractionContext ctx)
        {
            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            return async (_, e) =>
            {
                switch (e.Reason)
                {
                    case LavalinkTrackEndReason.Stopped:
                    case LavalinkTrackEndReason.Cleanup:
                        _queueManager.IsPlaying = false;
                        break;

                    case LavalinkTrackEndReason.Replaced:
                        _queueManager.IsPlaying = true;
                        break;

                    case LavalinkTrackEndReason.LoadFailed:
                        await new DiscordMessageBuilder()
                            .AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkLoadFailed))
                            .SendAsync(ctx.Channel);
                        EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Error, ["ERR_LAVALINK_LOAD_FAILED", $"Failed to load track {e.Track.Info.Title} ({e.Track.Info.Uri})"]);
                        _queueManager.IsPlaying = true;
                        await guildPlayer!.PlayAsync(_queueManager.Next()!.Track);
                        break;

                    case LavalinkTrackEndReason.Finished:
                        _queueManager.IsPlaying = false;
                        RadiTrack? nextTrack = _queueManager.Next();
                        if (nextTrack is null)
                        {
                            var msg = await new DiscordMessageBuilder()
                                .AddEmbed(EmbedGenerator.StatusEmbed(EmbedStatusType.StatusQueueEnd))
                                .SendAsync(ctx.Channel);
                            EventLogger.LogPlayerEvent(ctx.Guild!.Id, PlayerEventType.Stop);
                            await Task.Delay(10000);
                            await msg.DeleteAsync();
                        }
                        else
                        {
                            _queueManager.IsPlaying = true;
                            await guildPlayer!.PlayAsync(nextTrack.Track);
                        }

                        break;
                }
            };
        }

        #endregion

        /// <summary>
        /// Overrides the behavior after a slash command has been executed.
        /// This is for handling the internal call flag.
        /// </summary>
        /// <param name="ctx">The context of the interaction.</param>
        /// <returns>The successfully completed Task.</returns>
        public override Task<bool> AfterSlashExecutionAsync(InteractionContext ctx)
        {
            if (_internalCall)
                _internalCall = false;
            return Task.FromResult(true);
        }
    }
}