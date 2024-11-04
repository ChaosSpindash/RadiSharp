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
}