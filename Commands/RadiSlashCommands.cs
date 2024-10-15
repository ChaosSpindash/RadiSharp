using System.Text;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Interactivity.Extensions;

namespace RadiSharp.Commands
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class RadiSlashCommands : ApplicationCommandsModule
    {
        [SlashCommand("ping", "Pong!")]
        public async Task PingAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Pong!"));
        }
    }
}