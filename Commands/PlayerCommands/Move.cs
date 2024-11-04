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
}