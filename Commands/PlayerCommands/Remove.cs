using DisCatSharp.Entities;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.Lavalink;
using RadiSharp.Libraries.Managers;
using RadiSharp.Libraries.Utilities;
using RadiSharp.Typedefs;

namespace RadiSharp.Commands;

public partial class PlayerCommands
{
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
}