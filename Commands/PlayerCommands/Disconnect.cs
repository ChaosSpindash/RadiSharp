using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.ApplicationCommands.Attributes;

namespace RadiSharp.Commands;

public partial class PlayerCommands
{
    /// <summary>
    /// Disconnects from the voice channel.
    /// </summary>
    /// <param name="ctx">The context of the interaction.</param>
    /// <remarks>
    /// Many music bots have this as a (sometimes undocumented) alias for their Stop/Leave commands.
    /// Unfortunately, the introduction of slash commands to Discord made the use of traditional prefixed commands
    /// more and more obsolete, so this command can no longer be easily hidden from plain sight.
    /// It is still being kept here as a little easter egg.
    /// </remarks>
    [SlashCommand("fuckoff", "Leave the voice channel.")]
    public async Task LeaveAsync(InteractionContext ctx)
    {
        await StopAsync(ctx);
    }
}