using DisCatSharp.Entities;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Common.Utilities;
using DisCatSharp.Lavalink;
using DisCatSharp.Lavalink.Enums;
using DisCatSharp.Lavalink.EventArgs;
using RadiSharp.Libraries;
using RadiSharp.Libraries.Managers;
using RadiSharp.Libraries.Utilities;
using RadiSharp.Typedefs;

namespace RadiSharp.Commands
{
    // ReSharper disable once ClassNeverInstantiated.Global
    /// <summary>
    /// Contains commands for controlling the player and other related functions.
    /// </summary>
    public partial class PlayerCommands : ApplicationCommandsModule
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