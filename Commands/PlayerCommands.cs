using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.Lavalink;
using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Enums;
using System.Threading.Channels;
using RadiSharp.Libraries;
using DisCatSharp.Interactivity;
using DisCatSharp.Interactivity.Extensions;

namespace RadiSharp.Commands
{
    public class PlayerCommands : ApplicationCommandsModule
    {

        QueueManager queueManager = QueueManager.Instance;

        [SlashCommand("stop", "Stop playback, clear queue and leave the voice channel.")]
        public async Task LeaveAsync(InteractionContext ctx)
        {
            
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);
            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("No active connection to a Lavalink node.")
                    .WithColor(DiscordColor.Red)));
                return;
            }

            queueManager.Clear();
            await guildPlayer.DisconnectAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle("👋 Bye bye!")
                .WithDescription("Disconnected from the voice channel.")
                .WithColor(DiscordColor.Green)));
            await Task.Delay(10000);
            await ctx.DeleteResponseAsync();

        }

        [SlashCommand("play", "Play a track from an URL or search query.")]
        public async Task PlayAsync(InteractionContext ctx, [Option("query", "The query to search for.")] string query)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("You are not in a voice channel.")
                    .WithColor(DiscordColor.Red)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                var session = lavalink.ConnectedSessions.Values.First();
                if (ctx.Member.VoiceState.Channel.Type != ChannelType.Voice && ctx.Member.VoiceState.Channel.Type != ChannelType.Stage)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle("❌ Error")
                        .WithDescription("You are not in a valid voice channel.")
                        .WithColor(DiscordColor.Red)));
                    return;
                }

                await session.ConnectAsync(ctx.Member.VoiceState.Channel);
                guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

                guildPlayer!.TrackStarted += async (s, e) =>
                {
                    await new DiscordMessageBuilder()
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle("▶️ Now Playing")
                        .WithDescription($"[{e.Track.Info.Title}]({e.Track.Info.Uri})")
                        .WithThumbnail($"https://img.youtube.com/vi/{e.Track.Info.Identifier}/maxresdefault.jpg")
                        .WithColor(DiscordColor.Green)
                        .AddFields(
                            [
                                new DiscordEmbedField("Requested by", queueManager.CurrentTrack()!.RequestedBy.Mention ?? "Unknown", true),
                                new DiscordEmbedField("Duration", e.Track.Info.IsStream ? "`🔴 LIVE`" : $"`{e.Track.Info.Length}`", true)
                            ]
                        ))
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_previous", "", false, new DiscordComponentEmoji("⏮️")),
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_pause", "", false, new DiscordComponentEmoji("⏸️")),
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_skip", "", false, new DiscordComponentEmoji("⏭️")),
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_stop", "", false, new DiscordComponentEmoji("⏹️")),
                            new DiscordButtonComponent(queueManager.Repeat ? ButtonStyle.Success : ButtonStyle.Secondary, "player_repeat", "", false, new DiscordComponentEmoji("🔂"))
                        }).SendAsync(ctx.Channel);
                };

                ctx.Client.ComponentInteractionCreated += async (s, e) =>
                {
                    switch (e.Id)
                    {

                        case "player_previous":
                            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                            await guildPlayer.PlayAsync(queueManager.Previous()!.Track);
                            break;
                        case "player_skip":
                            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                            await guildPlayer.PlayAsync(queueManager.Next(true)!.Track);
                            break;
                        case "player_pause":
                            await guildPlayer.PauseAsync();
                            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("▶️ Now Playing")
                                .WithDescription($"[{queueManager.CurrentTrack()!.Track.Info.Title}]({queueManager.CurrentTrack()!.Track.Info.Uri})")
                                .WithThumbnail($"https://img.youtube.com/vi/{queueManager.CurrentTrack()!.Track.Info.Identifier}/maxresdefault.jpg")
                                .WithColor(DiscordColor.Yellow)
                                .AddFields(
                            [
                                new DiscordEmbedField("Requested by", queueManager.CurrentTrack()!.RequestedBy.Mention ?? "Unknown", true),
                                new DiscordEmbedField("Duration", queueManager.CurrentTrack()!.Track.Info.IsStream ? "`🔴 LIVE`" : $"`{queueManager.CurrentTrack()!.Track.Info.Length}`", true)
                            ]
                            ))
                            .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_previous", "", false, new DiscordComponentEmoji("⏮️")),
                            new DiscordButtonComponent(ButtonStyle.Success, "player_play", "", false, new DiscordComponentEmoji("▶️")),
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_skip", "", false, new DiscordComponentEmoji("⏭️")),
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_stop", "", false, new DiscordComponentEmoji("⏹️")),
                            new DiscordButtonComponent(queueManager.Repeat ? ButtonStyle.Success : ButtonStyle.Secondary, "player_repeat", "", false, new DiscordComponentEmoji("🔂"))
                        })
                            );
                            break;
                        case "player_play":
                            await guildPlayer.ResumeAsync();
                            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("▶️ Now Playing")
                                .WithDescription($"[{queueManager.CurrentTrack()!.Track.Info.Title}]({queueManager.CurrentTrack()!.Track.Info.Uri})")
                                .WithThumbnail($"https://img.youtube.com/vi/{queueManager.CurrentTrack()!.Track.Info.Identifier}/maxresdefault.jpg")
                                .WithColor(DiscordColor.Green)
                                .AddFields(
                            [
                                new DiscordEmbedField("Requested by", queueManager.CurrentTrack()!.RequestedBy.Mention ?? "Unknown", true),
                                new DiscordEmbedField("Duration", queueManager.CurrentTrack()!.Track.Info.IsStream ? "`🔴 LIVE`" : $"`{queueManager.CurrentTrack()!.Track.Info.Length}`", true)
                            ]
                            ))
                            .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_previous", "", false, new DiscordComponentEmoji("⏮️")),
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_pause", "", false, new DiscordComponentEmoji("⏸️")),
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_skip", "", false, new DiscordComponentEmoji("⏭️")),
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_stop", "", false, new DiscordComponentEmoji("⏹️")),
                            new DiscordButtonComponent(queueManager.Repeat ? ButtonStyle.Success : ButtonStyle.Secondary, "player_repeat", "", false, new DiscordComponentEmoji("🔂"))
                        })
                            );
                            break;
                        case "player_stop":
                            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                            queueManager.Clear();
                            await guildPlayer.DisconnectAsync();
                            var msg = await new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("👋 Bye bye!")
                                .WithDescription("Disconnected from the voice channel.")
                                .WithColor(DiscordColor.Green))
                                .SendAsync(ctx.Channel);
                            await Task.Delay(10000);
                            await msg.DeleteAsync();
                            break;
                        case "player_repeat":
                            queueManager.ToggleRepeat();
                            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("▶️ Now Playing")
                                .WithDescription($"[{queueManager.CurrentTrack()!.Track.Info.Title}]({queueManager.CurrentTrack()!.Track.Info.Uri})")
                                .WithThumbnail($"https://img.youtube.com/vi/{queueManager.CurrentTrack()!.Track.Info.Identifier}/maxresdefault.jpg")
                                .WithColor(DiscordColor.Green)
                                .AddFields(
                            [
                                new DiscordEmbedField("Requested by", queueManager.CurrentTrack()!.RequestedBy.Mention ?? "Unknown", true),
                                new DiscordEmbedField("Duration", queueManager.CurrentTrack()!.Track.Info.IsStream ? "`🔴 LIVE`" : $"`{queueManager.CurrentTrack()!.Track.Info.Length}`", true)
                            ]
                            ))
                            .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_previous", "", false, new DiscordComponentEmoji("⏮️")),
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_pause", "", false, new DiscordComponentEmoji("⏸️")),
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_skip", "", false, new DiscordComponentEmoji("⏭️")),
                            new DiscordButtonComponent(ButtonStyle.Primary, "player_stop", "", false, new DiscordComponentEmoji("⏹️")),
                            new DiscordButtonComponent(queueManager.Repeat ? ButtonStyle.Success : ButtonStyle.Secondary, "player_repeat", "", false, new DiscordComponentEmoji("🔂"))
                        })
                            );
                            break;
                    }
                };

                guildPlayer.TrackEnded += async (s, e) =>
                {
                    switch (e.Reason)
                    {
                        case LavalinkTrackEndReason.Stopped:
                        case LavalinkTrackEndReason.Replaced:
                        case LavalinkTrackEndReason.Cleanup:
                            break;
                    
                        case LavalinkTrackEndReason.LoadFailed:
                            await new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("❌ Error")
                                .WithDescription("Failed to load the next track.")
                                .WithColor(DiscordColor.Red))
                                .SendAsync(ctx.Channel);
                            await guildPlayer.PlayAsync(queueManager.Next()!.Track);
                            break;
                    
                        case LavalinkTrackEndReason.Finished:
                            RadiTrack? nextTrack = queueManager.Next();
                            if (nextTrack is null)
                            {
                                var msg = await new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                                    .WithTitle("🏁 End of Queue")
                                    .WithDescription("No more tracks to play.")
                                    .WithColor(DiscordColor.Green))
                                    .SendAsync(ctx.Channel);
                                await Task.Delay(10000);
                                await msg.DeleteAsync();
                                break;
                            }
                            else
                            {
                                await guildPlayer.PlayAsync(nextTrack.Track);
                            }
                            break;
                    }
                };
            }

            var loadResult = await guildPlayer.LoadTracksAsync(query);

            if (loadResult.LoadType == LavalinkLoadResultType.Empty || loadResult.LoadType == LavalinkLoadResultType.Error)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription($"No match found for {query}")
                    .WithColor(DiscordColor.Red)));
                return;
            }

            if (loadResult.LoadType == LavalinkLoadResultType.Playlist)
            {
                LavalinkPlaylist playlist = loadResult.GetResultAs<LavalinkPlaylist>();

                queueManager.AddPlaylist(new RadiPlaylist(playlist, ctx.Member));

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("📝 Added Playlist to Queue")
                    .WithDescription($"[{playlist.Info.Name}]({playlist.Tracks.First().Info.Uri})")
                    .WithThumbnail($"https://img.youtube.com/vi/{playlist.Tracks.First().Info.Identifier}/maxresdefault.jpg")
                    .WithColor(DiscordColor.Green)
                    .AddFields(
                        [
                            new DiscordEmbedField("Requested by", ctx.Member.Mention ?? "Unknown", true),
                        new DiscordEmbedField("Tracks", $"{playlist.Tracks.Count.ToString()}", true)
                        ])));
            }
            else
            {
                LavalinkTrack track = loadResult.LoadType switch
                {
                    LavalinkLoadResultType.Track => loadResult.GetResultAs<LavalinkTrack>(),
                    LavalinkLoadResultType.Search => loadResult.GetResultAs<List<LavalinkTrack>>().First(),
                    _ => throw new InvalidOperationException("Unexpected load result type.")
                };

                queueManager.Add(new RadiTrack(track, ctx.Member));

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("📝 Added to Queue")
                    .WithDescription($"[{track.Info.Title}]({track.Info.Uri})")
                    .WithThumbnail($"https://img.youtube.com/vi/{track.Info.Identifier}/maxresdefault.jpg")
                    .WithColor(DiscordColor.Green)
                    .AddFields(
                    [
                        new DiscordEmbedField("Requested by", ctx.Member.Mention ?? "Unknown", true),
                    new DiscordEmbedField("Duration", track.Info.IsStream ? "`🔴 LIVE`" : $"`{track.Info.Length}`", true)
                    ])));
            }



            await guildPlayer.PlayAsync(queueManager.Next()!.Track);

        }

        [SlashCommand("pause", "Pause the current track.")]
        public async Task PauseAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                   .WithTitle("❌ Error")
                   .WithDescription("You are not in a voice channel.")
                   .WithColor(DiscordColor.Red)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("No active connection to a Lavalink node.")
                    .WithColor(DiscordColor.Red)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("No tracks in queue.")
                    .WithColor(DiscordColor.Red)));
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
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("You are not in a voice channel.")
                    .WithColor(DiscordColor.Red)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("No active connection to a Lavalink node.")
                    .WithColor(DiscordColor.Red)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("No tracks in queue.")
                    .WithColor(DiscordColor.Red)));
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
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                   .WithTitle("❌ Error")
                   .WithDescription("You are not in a voice channel.")
                   .WithColor(DiscordColor.Red)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("No active connection to a Lavalink node.")
                    .WithColor(DiscordColor.Red)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("No tracks in queue.")
                    .WithColor(DiscordColor.Red)));
                return;
            }
            var desc = queueManager.GetPlaylist();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle("📜 Queue")
                .WithDescription(desc)
                .WithColor(DiscordColor.Green)
                .AddFields(
                [
                    new DiscordEmbedField("Total Tracks", $"`{queueManager.PlaylistCount()}`", true),
                    new DiscordEmbedField("Current Track Position", $"`{queueManager.CurrentTrack()!.Track.Info.Position} / {queueManager.CurrentTrack()!.Track.Info.Length}", true),
                    new DiscordEmbedField("Total Duration", $"`{queueManager.CurrentTrack()!.Track.Info.Length}`", true)
                ])
                .WithFooter($"Page {queueManager.PageCurrent}/{queueManager.PageCount}"))
                .AddComponents(new DiscordComponent[]
                {
                    new DiscordButtonComponent(ButtonStyle.Primary, "queue_previous_page", "", false, new DiscordComponentEmoji("⏮️")),
                    new DiscordButtonComponent(ButtonStyle.Primary, "queue_next_page", "", false, new DiscordComponentEmoji("⏭️")),
                    new DiscordButtonComponent(queueManager.LoopQueue ? ButtonStyle.Success : ButtonStyle.Secondary, "queue_loop", "", false, new DiscordComponentEmoji("🔁")),
                    new DiscordButtonComponent(queueManager.Shuffle ? ButtonStyle.Success : ButtonStyle.Secondary, "queue_shuffle", "", false, new DiscordComponentEmoji("🔀")),
                    new DiscordButtonComponent(ButtonStyle.Danger, "queue_clear", "", false, new DiscordComponentEmoji("🗑️"))
                })
                );

            ctx.Client.ComponentInteractionCreated += async (s, e) =>
            {
                switch (e.Id)
                {
                    case "queue_clear":
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                        queueManager.Clear();
                        await guildPlayer.StopAsync();
                        await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("🗑️ Cleared Queue")
                            .WithDescription("The queue has been cleared.")
                            .WithColor(DiscordColor.Green)));
                        await Task.Delay(10000);
                        await e.Interaction.DeleteOriginalResponseAsync();
                        break;
                    case "queue_loop":
                        queueManager.ToggleLoopQueue();
                        var descLoop = queueManager.GetPlaylist(queueManager.PageCurrent);
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("📜 Queue")
                            .WithDescription(descLoop)
                            .WithColor(DiscordColor.Green)
                            .AddFields(
                            [
                                new DiscordEmbedField("Total Tracks", $"`{queueManager.PlaylistCount()}`", true),
                                new DiscordEmbedField("Current Track Position", $"`{queueManager.CurrentTrack()!.Track.Info.Position} / {queueManager.CurrentTrack()!.Track.Info.Length}", true),
                                new DiscordEmbedField("Total Duration", $"`{queueManager.CurrentTrack()!.Track.Info.Length}`", true)
                            ])
                            .WithFooter($"Page {queueManager.PageCurrent}/{queueManager.PageCount}"))
                            .AddComponents(new DiscordComponent[]
                            {
                                new DiscordButtonComponent(ButtonStyle.Primary, "queue_previous_page", "", false, new DiscordComponentEmoji("⏮️")),
                                new DiscordButtonComponent(ButtonStyle.Primary, "queue_next_page", "", false, new DiscordComponentEmoji("⏭️")),
                                new DiscordButtonComponent(queueManager.LoopQueue ? ButtonStyle.Success : ButtonStyle.Secondary, "queue_loop", "", false, new DiscordComponentEmoji("🔁")),
                                new DiscordButtonComponent(queueManager.Shuffle ? ButtonStyle.Success : ButtonStyle.Secondary, "queue_shuffle", "", false, new DiscordComponentEmoji("🔀")),
                                new DiscordButtonComponent(ButtonStyle.Danger, "queue_clear", "", false, new DiscordComponentEmoji("🗑️"))
                            }));
                        break;
                    case "queue_shuffle":
                        queueManager.ToggleShuffle();
                        var descShuffle = queueManager.GetPlaylist(queueManager.PageCurrent);
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("📜 Queue")
                            .WithDescription(descShuffle)
                            .WithColor(DiscordColor.Green)
                            .AddFields(
                            [
                                new DiscordEmbedField("Total Tracks", $"`{queueManager.PlaylistCount()}`", true),
                                new DiscordEmbedField("Current Track Position", $"`{queueManager.CurrentTrack()!.Track.Info.Position} / {queueManager.CurrentTrack()!.Track.Info.Length}", true),
                                new DiscordEmbedField("Total Duration", $"`{queueManager.CurrentTrack()!.Track.Info.Length}`", true)
                            ])
                            .WithFooter($"Page {queueManager.PageCurrent}/{queueManager.PageCount}"))
                            .AddComponents(new DiscordComponent[]
                            {
                                new DiscordButtonComponent(ButtonStyle.Primary, "queue_previous_page", "", false, new DiscordComponentEmoji("⏮️")),
                                new DiscordButtonComponent(ButtonStyle.Primary, "queue_next_page", "", false, new DiscordComponentEmoji("⏭️")),
                                new DiscordButtonComponent(queueManager.LoopQueue ? ButtonStyle.Success : ButtonStyle.Secondary, "queue_loop", "", false, new DiscordComponentEmoji("🔁")),
                                new DiscordButtonComponent(queueManager.Shuffle ? ButtonStyle.Success : ButtonStyle.Secondary, "queue_shuffle", "", false, new DiscordComponentEmoji("🔀")),
                                new DiscordButtonComponent(ButtonStyle.Danger, "queue_clear", "", false, new DiscordComponentEmoji("🗑️"))
                            }));
                        break;
                    case "queue_previous_page":
                        var descPrev = queueManager.GetPlaylist(queueManager.PageCurrent - 1);
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("📜 Queue")
                            .WithDescription(descPrev)
                            .WithColor(DiscordColor.Green)
                            .AddFields(
                            [
                                new DiscordEmbedField("Total Tracks", $"`{queueManager.PlaylistCount()}`", true),
                                new DiscordEmbedField("Current Track Position", $"`{queueManager.CurrentTrack()!.Track.Info.Position} / {queueManager.CurrentTrack()!.Track.Info.Length}", true),
                                new DiscordEmbedField("Total Duration", $"`{queueManager.CurrentTrack()!.Track.Info.Length}`", true)
                            ])
                            .WithFooter($"Page {queueManager.PageCurrent}/{queueManager.PageCount}"))
                            .AddComponents(new DiscordComponent[]
                            {
                                new DiscordButtonComponent(ButtonStyle.Primary, "queue_previous_page", "", false, new DiscordComponentEmoji("⏮️")),
                                new DiscordButtonComponent(ButtonStyle.Primary, "queue_next_page", "", false, new DiscordComponentEmoji("⏭️")),
                                new DiscordButtonComponent(ButtonStyle.Secondary, "queue_loop", "", false, new DiscordComponentEmoji("🔁")),
                                new DiscordButtonComponent(ButtonStyle.Secondary, "queue_shuffle", "", false, new DiscordComponentEmoji("🔀")),
                                new DiscordButtonComponent(ButtonStyle.Danger, "queue_clear", "", false, new DiscordComponentEmoji("🗑️"))
                            }));
                        break;
                    case "queue_next_page":
                        var descNext = queueManager.GetPlaylist(queueManager.PageCurrent + 1);
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("📜 Queue")
                            .WithDescription(descNext)
                            .WithColor(DiscordColor.Green)
                            .AddFields(
                            [
                                new DiscordEmbedField("Total Tracks", $"`{queueManager.PlaylistCount()}`", true),
                                new DiscordEmbedField("Current Track Position", $"`{queueManager.CurrentTrack()!.Track.Info.Position} / {queueManager.CurrentTrack()!.Track.Info.Length}", true),
                                new DiscordEmbedField("Total Duration", $"`{queueManager.CurrentTrack()!.Track.Info.Length}`", true)
                            ])
                            .WithFooter($"Page {queueManager.PageCurrent}/{queueManager.PageCount}"))
                            .AddComponents(new DiscordComponent[]
                            {
                                new DiscordButtonComponent(ButtonStyle.Primary, "queue_previous_page", "", false, new DiscordComponentEmoji("⏮️")),
                                new DiscordButtonComponent(ButtonStyle.Primary, "queue_next_page", "", false, new DiscordComponentEmoji("⏭️")),
                                new DiscordButtonComponent(ButtonStyle.Secondary, "queue_loop", "", false, new DiscordComponentEmoji("🔁")),
                                new DiscordButtonComponent(ButtonStyle.Secondary, "queue_shuffle", "", false, new DiscordComponentEmoji("🔀")),
                                new DiscordButtonComponent(ButtonStyle.Danger, "queue_clear", "", false, new DiscordComponentEmoji("🗑️"))
                            }));
                        break;
                }
            };
        }

        [SlashCommand("skip", "Skip the current track.")]
        public async Task SkipAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                                      .WithTitle("❌ Error")
                                                        .WithDescription("You are not in a voice channel.")
                                                                          .WithColor(DiscordColor.Red)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                                       .WithTitle("❌ Error")
                                                          .WithDescription("No active connection to a Lavalink node.")
                                                                             .WithColor(DiscordColor.Red)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                                       .WithTitle("❌ Error")
                                                          .WithDescription("No tracks in queue.")
                                                                             .WithColor(DiscordColor.Red)));
                return;
            }

            await guildPlayer.PlayAsync(queueManager.Next(true)!.Track);
            await ctx.DeleteResponseAsync();
        }

        [SlashCommand("clear", "Clear the current queue.")]
        public async Task ClearAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            if (ctx.Member?.VoiceState?.Channel is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                   .WithTitle("❌ Error")
                   .WithDescription("You are not in a voice channel.")
                   .WithColor(DiscordColor.Red)));
                return;
            }

            var lavalink = ctx.Client.GetLavalink();
            var guildPlayer = lavalink.GetGuildPlayer(ctx.Guild!);

            if (guildPlayer is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("No active connection to a Lavalink node.")
                    .WithColor(DiscordColor.Red)));
                return;
            }

            if (guildPlayer.CurrentTrack is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("No tracks in queue.")
                    .WithColor(DiscordColor.Red)));
                return;
            }

            queueManager.Clear();
            await guildPlayer.StopAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                               .WithTitle("🗑️ Cleared Queue")
                                              .WithDescription("The queue has been cleared.")
                                                             .WithColor(DiscordColor.Green)));
            await Task.Delay(10000);
            await ctx.DeleteResponseAsync();
        }
    }
}
