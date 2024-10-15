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
using YTSearch.NET;

namespace RadiSharp.Commands
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerCommands : ApplicationCommandsModule
    {
        private readonly QueueManager _queueManager = QueueManager.Instance;
        private AsyncEventHandler<LavalinkGuildPlayer, LavalinkTrackStartedEventArgs>? _trackStartedHandler;
        private AsyncEventHandler<LavalinkGuildPlayer, LavalinkTrackEndedEventArgs>? _trackEndedHandler;
        private bool _internalCall;

        [SlashCommand("stop", "Stop playback, clear queue and leave the voice channel.")]
        // ReSharper disable once MemberCanBePrivate.Global
        public async Task StopAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);
            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkNoSession)));
                return;
            }

            _queueManager.Clear();
            await guildPlayer.DisconnectAsync();
            if (_trackStartedHandler != null) guildPlayer.TrackStarted -= _trackStartedHandler;
            if (_trackEndedHandler != null) guildPlayer.TrackEnded -= _trackEndedHandler;
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.StatusEmbed(EmbedStatusType.StatusDisconnect)));
            await Task.Delay(10000);
            await ctx.DeleteResponseAsync();
        }

        [SlashCommand("play", "Play a track from an URL or search query.")]
        public async Task PlayAsync(InteractionContext ctx, [Option("query", "The query to search for.")] string? query)
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
                }
                else
                {
                    LavalinkTrack track = loadResult.LoadType switch
                    {
                        LavalinkLoadResultType.Track => loadResult.GetResultAs<LavalinkTrack>(),
                        LavalinkLoadResultType.Search => loadResult.GetResultAs<List<LavalinkTrack>>().First(),
                        _ => throw new InvalidOperationException("Unexpected load result type.")
                    };

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
                }
            }


            if (!_queueManager.IsPlaying)
            {
                _queueManager.IsPlaying = true;
                await guildPlayer.PlayAsync(_queueManager.Next()!.Track);
            }
        }

        // ReSharper disable once UnusedMember.Local
        private async Task DisconnectIdlePlayer(InteractionContext ctx, LavalinkGuildPlayer guildPlayer)
        {
            _queueManager.Clear();
            await guildPlayer.DisconnectAsync();
            if (_trackStartedHandler != null) guildPlayer.TrackStarted -= _trackStartedHandler;
            if (_trackEndedHandler != null) guildPlayer.TrackEnded -= _trackEndedHandler;
            var msg = await new DiscordMessageBuilder()
                .AddEmbed(EmbedGenerator.StatusEmbed(EmbedStatusType.StatusInactivity)).SendAsync(ctx.Channel);
            await Task.Delay(10000);
            await msg.DeleteAsync();
        }

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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
                return;
            }

            if (guildPlayer.Player.Paused)
            {
                await ctx.DeleteResponseAsync();
                return;
            }

            await guildPlayer.PauseAsync();
            await ctx.DeleteResponseAsync();
        }

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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
                return;
            }

            if (!guildPlayer.Player.Paused)
            {
                await ctx.DeleteResponseAsync();
                return;
            }

            await guildPlayer.ResumeAsync();
            await ctx.DeleteResponseAsync();
        }

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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
                return;
            }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .AddEmbed(EmbedGenerator.QueueEmbed(_queueManager, guildPlayer))
                .AddComponents(EmbedGenerator.QueueComponents(_queueManager)));
        }

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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
                return;
            }

            await guildPlayer.PlayAsync(_queueManager.Next(true)!.Track);
            await ctx.DeleteResponseAsync();
        }

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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
                return;
            }

            _queueManager.Clear();
            await guildPlayer.StopAsync();
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.StatusEmbed(EmbedStatusType.StatusClearQueue)));
            await Task.Delay(10000);
            await ctx.DeleteResponseAsync();
        }

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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
                return;
            }

            await guildPlayer.PlayAsync(_queueManager.Previous()!.Track);
            await ctx.DeleteResponseAsync();
        }

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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
                return;
            }

            _queueManager.ToggleLoop();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"🔂 Loop {(_queueManager.Loop ? "Enabled" : "Disabled")}")
                .WithColor(DiscordColor.Green)));
        }

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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
                return;
            }

            _queueManager.ToggleShuffle();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"🔀 Shuffle {(_queueManager.Shuffle ? "Enabled" : "Disabled")}")
                .WithColor(DiscordColor.Green)));
        }

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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
                return;
            }

            _queueManager.ToggleLoopQueue();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"🔁 Loop Queue {(_queueManager.LoopQueue ? "Enabled" : "Disabled")}")
                .WithColor(DiscordColor.Green)));
        }

        [SlashCommand("remove", "Remove a track from the queue.")]
        public async Task RemoveAsync(InteractionContext ctx,
            [Option("index", "The index of the track to remove.")] int index)
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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
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
        }

        [SlashCommand("move", "Move a track in the queue.")]
        public async Task MoveAsync(InteractionContext ctx,
            [Option("from", "The index of the track to move.")] int from,
            [Option("to", "The index to move the track to.")] int to)
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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
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
        }

        [SlashCommand("skipto", "Skip to a specific track in the queue.")]
        public async Task SkipToAsync(InteractionContext ctx,
            [Option("index", "The index of the track to skip to.")] int index)
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
                        EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrLavalinkQueueEmpty)));
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

        [SlashCommand("fuckoff", "Leave the voice channel.")]
        public async Task LeaveAsync(InteractionContext ctx)
        {
            await StopAsync(ctx);
        }

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

            var interactivity = ctx.Client.GetInteractivity();
            var result = interactivity.WaitForMessageAsync(x => x.Author.Id == ctx.UserId);
            if (!result.Result.TimedOut)
            {
                if(Int32.TryParse(result.Result.Result.Content, out int index))
                {
                    if (index > 0 && index <= videos.Count)
                    {
                        _internalCall = true;
                        await PlayAsync(ctx, videos[index - 1]);
                    }
                    else
                    {
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrIndexOutOfRange)));
                    }
                }
                else
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrInvalidArg)));
                }
            }

        }

        #region Event Handlers
        
        private AsyncEventHandler<LavalinkGuildPlayer, LavalinkTrackStartedEventArgs> CreateTrackStartedHandler(
            InteractionContext ctx)
        {
            return async (_, _) =>
            {
                await new DiscordMessageBuilder()
                    .AddEmbed(EmbedGenerator.PlayerEmbed(_queueManager))
                    .AddComponents(EmbedGenerator.PlayerComponents(_queueManager))
                    .SendAsync(ctx.Channel);
            };
        }

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
        
        public override Task<bool> AfterSlashExecutionAsync(InteractionContext ctx)
        {
            if (_internalCall)
                _internalCall = false;
            return Task.FromResult(true);
        }
    }
}