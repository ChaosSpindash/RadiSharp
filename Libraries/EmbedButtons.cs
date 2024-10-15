using DisCatSharp.Entities;
using DisCatSharp.EventArgs;
using DisCatSharp.Lavalink;

namespace RadiSharp.Libraries;

[EventHandler]
// ReSharper disable once ClassNeverInstantiated.Global
public class EmbedButtons
{
    [Event(DiscordEvent.ComponentInteractionCreated)]
    public async Task QueueEmbedButtonsEventHandler(DiscordClient s, ComponentInteractionCreateEventArgs e)
    {
        var ll = s.GetLavalink();
        var gp = ll.GetGuildPlayer(e.Guild);
        if (gp is null || !gp.Channel.Users.Contains(e.User))
        {
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
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
                        await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(EmbedGenerator.StatusEmbed(EmbedStatusType.StatusClearQueue)));
                        await Task.Delay(10000);
                        await e.Interaction.DeleteOriginalResponseAsync();
                        break;
                    case "queue_loop":
                        queueManager.ToggleLoopQueue();
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                            .AddEmbed(EmbedGenerator.QueueEmbed(queueManager, gp))
                            .AddComponents(EmbedGenerator.QueueComponents(queueManager)));
                        break;
                    case "queue_shuffle":
                        queueManager.ToggleShuffle();
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                            .AddEmbed(EmbedGenerator.QueueEmbed(queueManager, gp))
                            .AddComponents(EmbedGenerator.QueueComponents(queueManager)));
                        break;
                    case "queue_previous_page":
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                            .AddEmbed(EmbedGenerator.QueueEmbed(queueManager, gp, -1))
                            .AddComponents(EmbedGenerator.QueueComponents(queueManager)));
                        break;
                    case "queue_next_page":
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                            .AddEmbed(EmbedGenerator.QueueEmbed(queueManager, gp, 1))
                            .AddComponents(EmbedGenerator.QueueComponents(queueManager)));
                        break;
                }
    }
    
    [Event(DiscordEvent.ComponentInteractionCreated)]
    public async Task PlayerEmbedButtonsEventHandler(DiscordClient s, ComponentInteractionCreateEventArgs e)
    {
        var ll = s.GetLavalink();
        var gp = ll.GetGuildPlayer(e.Guild);
        if (gp is null || !gp.Channel.Users.Contains(e.User))
        {
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
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
                            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                                .AddEmbed(EmbedGenerator.PlayerEmbed(queueManager, true))
                                .AddComponents(EmbedGenerator.PlayerComponents(queueManager, true))
                            );
                            break;
                        case "player_resume":
                            await gp.ResumeAsync();
                            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                                .AddEmbed(EmbedGenerator.PlayerEmbed(queueManager))
                                .AddComponents(EmbedGenerator.PlayerComponents(queueManager))
                            );
                            break;
                        case "player_stop":
                            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                            queueManager.Clear();
                            await gp.DisconnectAsync();
                            var msg = await new DiscordMessageBuilder().AddEmbed(EmbedGenerator.StatusEmbed(EmbedStatusType.StatusDisconnect)).SendAsync(e.Channel);
                            await Task.Delay(10000);
                            await msg.DeleteAsync();
                            break;
                        case "player_loop":
                            queueManager.ToggleLoop();
                            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                                .AddEmbed(EmbedGenerator.PlayerEmbed(queueManager))
                                .AddComponents(EmbedGenerator.PlayerComponents(queueManager))
                            );
                            break;
                    }
    }
}