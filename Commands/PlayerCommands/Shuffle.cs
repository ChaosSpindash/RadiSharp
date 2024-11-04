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
}