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

        QueueManager queueManager = new QueueManager();

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

            await guildPlayer.DisconnectAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle("👋 Bye bye!")
                .WithDescription("Disconnected from the voice channel.")
                .WithColor(DiscordColor.Green)));
            await Task.Delay(10000);
            await ctx.DeleteResponseAsync();
            queueManager.Clear(true);
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
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
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
                            new DiscordButtonComponent(ButtonStyle.Secondary, "player_repeat", "", false, new DiscordComponentEmoji("🔂"))
                        })
                );
                };

                guildPlayer.TrackEnded += async (s, e) =>
                {
                    RadiTrack? nextTrack = queueManager.Next();

                    if (nextTrack is null)
                    {
                        if (!queueManager.ManualClear)
                        {
                            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("🏁 End of Queue")
                                .WithDescription("No more tracks to play.")
                                .WithColor(DiscordColor.Green)));
                            await Task.Delay(10000);
                            await ctx.DeleteResponseAsync();
                        }
                        return;
                    }

                    await guildPlayer.PlayAsync(nextTrack.Track);
                };
            }

            var loadResult = await guildPlayer.LoadTracksAsync(LavalinkSearchType.Youtube, query);

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
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle("📜 Queue")
                .WithDescription(queueManager.GetPlaylist())
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
                    new DiscordButtonComponent(ButtonStyle.Primary, "queue_previous", "", false, new DiscordComponentEmoji("⏮️")),  
                    new DiscordButtonComponent(ButtonStyle.Primary, "queue_next", "", false, new DiscordComponentEmoji("⏭️")),
                    new DiscordButtonComponent(ButtonStyle.Secondary, "queue_loop", "", false, new DiscordComponentEmoji("🔁")),
                    new DiscordButtonComponent(ButtonStyle.Secondary, "queue_shuffle", "", false, new DiscordComponentEmoji("🔀")),
                    new DiscordButtonComponent(ButtonStyle.Danger, "queue_clear", "", false, new DiscordComponentEmoji("🗑️"))
                })
                );
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

            queueManager.Clear(true);
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
