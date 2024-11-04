using DisCatSharp.Entities;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.Lavalink;
using RadiSharp.Libraries.Utilities;
using RadiSharp.Typedefs;

namespace RadiSharp.Commands;

public partial class PlayerCommands
{
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
}