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

}