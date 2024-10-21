using DisCatSharp.Entities;
using DisCatSharp.EventArgs;
using DisCatSharp.Lavalink;
using RadiSharp.Libraries.Managers;
using RadiSharp.Typedefs;

namespace RadiSharp.Libraries.Utilities;

/// <summary>
/// Contains event handlers for the embed buttons.
/// </summary>
[EventHandler]
// ReSharper disable once ClassNeverInstantiated.Global
public class EmbedButtons
{
    /// <summary>
    /// Event handler for the queue embed buttons.
    /// </summary>
    /// <param name="s">The Discord client the event was triggered from.</param>
    /// <param name="e">The arguments for the ComponentInteractionCreate event.</param>
    [Event(DiscordEvent.ComponentInteractionCreated)]
    public async Task QueueEmbedButtonsEventHandler(DiscordClient s, ComponentInteractionCreateEventArgs e)
    {
        var ll = s.GetLavalink();
        var gp = ll.GetGuildPlayer(e.Guild);
        if (gp is null || !gp.Channel.Users.Contains(e.User))
        {
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AsEphemeral()
                    .AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
            return;
        }

        var queueManager = QueueManager.Instance;

        switch (e.Id)
        {
            case "queue_clear":
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                queueManager.Clear();
                await gp.StopAsync();
                await e.Interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.StatusEmbed(EmbedStatusType.StatusClearQueue)));
                EventLogger.LogPlayerEvent(e.Guild.Id, PlayerEventType.Clear);
                await Task.Delay(10000);
                await e.Interaction.DeleteOriginalResponseAsync();
                break;
            case "queue_loop":
                queueManager.ToggleLoopQueue();
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(EmbedGenerator.QueueEmbed(queueManager))
                        .AddComponents(EmbedGenerator.QueueComponents(queueManager)));
                EventLogger.LogPlayerEvent(e.Guild.Id, PlayerEventType.LoopQueue, [queueManager.LoopQueue ? "Enabled" : "Disabled"]);
                break;
            case "queue_shuffle":
                queueManager.ToggleShuffle();
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(EmbedGenerator.QueueEmbed(queueManager))
                        .AddComponents(EmbedGenerator.QueueComponents(queueManager)));
                EventLogger.LogPlayerEvent(e.Guild.Id, PlayerEventType.Shuffle, [queueManager.Shuffle ? "Enabled" : "Disabled"]);
                break;
            case "queue_previous_page":
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(EmbedGenerator.QueueEmbed(queueManager, -1))
                        .AddComponents(EmbedGenerator.QueueComponents(queueManager)));
                break;
            case "queue_next_page":
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(EmbedGenerator.QueueEmbed(queueManager, 1))
                        .AddComponents(EmbedGenerator.QueueComponents(queueManager)));
                break;
        }
    }

    /// <summary>
    /// Event handler for the player embed buttons.
    /// </summary>
    /// <param name="s">The Discord client the event was triggered from.</param>
    /// <param name="e">The arguments for the ComponentInteractionCreate event.</param>
    [Event(DiscordEvent.ComponentInteractionCreated)]
    public async Task PlayerEmbedButtonsEventHandler(DiscordClient s, ComponentInteractionCreateEventArgs e)
    {
        var ll = s.GetLavalink();
        var gp = ll.GetGuildPlayer(e.Guild);
        if (gp is null || !gp.Channel.Users.Contains(e.User))
        {
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AsEphemeral()
                    .AddEmbed(EmbedGenerator.ErrorEmbed(EmbedErrorType.ErrUserNotInVoice)));
            return;
        }

        var queueManager = QueueManager.Instance;

        switch (e.Id)
        {
            case "player_previous":
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                await gp.PlayAsync(queueManager.Previous()!.Track);
                break;
            case "player_skip":
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                await gp.PlayAsync(queueManager.Next(true)!.Track);
                break;
            case "player_pause":
                await gp.PauseAsync();
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(EmbedGenerator.PlayerEmbed(queueManager, true))
                        .AddComponents(EmbedGenerator.PlayerComponents(queueManager, true))
                );
                EventLogger.LogPlayerEvent(e.Guild.Id, PlayerEventType.Pause);
                break;
            case "player_resume":
                await gp.ResumeAsync();
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(EmbedGenerator.PlayerEmbed(queueManager))
                        .AddComponents(EmbedGenerator.PlayerComponents(queueManager))
                );
                EventLogger.LogPlayerEvent(e.Guild.Id, PlayerEventType.Resume);
                break;
            case "player_stop":
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                queueManager.Clear();
                await gp.DisconnectAsync();
                var msg = await new DiscordMessageBuilder()
                    .AddEmbed(EmbedGenerator.StatusEmbed(EmbedStatusType.StatusDisconnect)).SendAsync(e.Channel);
                EventLogger.LogPlayerEvent(e.Guild.Id, PlayerEventType.Disconnect);
                await Task.Delay(10000);
                await msg.DeleteAsync();
                break;
            case "player_loop":
                queueManager.ToggleLoop();
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(EmbedGenerator.PlayerEmbed(queueManager))
                        .AddComponents(EmbedGenerator.PlayerComponents(queueManager))
                );
                EventLogger.LogPlayerEvent(e.Guild.Id, PlayerEventType.Loop, [queueManager.Loop ? "Enabled" : "Disabled"]);
                break;
        }
    }
}