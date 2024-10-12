﻿using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;

namespace RadiSharp.Commands
{
    public abstract class RadiSlashCommands : ApplicationCommandsModule
    {
        [SlashCommandGroup("util", "Utility commands")]
        public class CommandGroupUtil : ApplicationCommandsModule
        {
            [SlashCommand("ping", "Pong!")]
            public async Task PingAsync(InteractionContext ctx)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Pong!"));
            }
        }
    }
}
