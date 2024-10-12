using DisCatSharp.Entities;

namespace RadiSharp.Libraries;

public static class EmbedGenerator
{
    public static DiscordEmbedBuilder ErrorEmbed(EmbedErrorType errorType)
    {
        switch (errorType)
        {
            case EmbedErrorType.ErrUserNotInVoice:
                return new DiscordEmbedBuilder()
                    .WithTitle("❌ You are not in a voice channel.")
                    .WithColor(DiscordColor.Red)
                    .WithFooter("ERR_USER_NOT_IN_VOICE");
            case EmbedErrorType.ErrLavalinkNoSession:
                return new DiscordEmbedBuilder()
                    .WithTitle("❌ No active session found.")
                    .WithColor(DiscordColor.Red)
                    .WithFooter("ERR_LAVALINK_NO_SESSION");
            case EmbedErrorType.ErrLavalinkLoadFailed:
                return new DiscordEmbedBuilder()
                    .WithTitle("❌ Failed to load the track.")
                    .WithColor(DiscordColor.Red)
                    .WithFooter("ERR_LAVALINK_LOAD_FAILED");
            case EmbedErrorType.ErrLavalinkQueueEmpty:
                return new DiscordEmbedBuilder()
                    .WithTitle("❌ Queue is empty.")
                    .WithColor(DiscordColor.Red)
                    .WithFooter("ERR_LAVALINK_QUEUE_EMPTY");
            case EmbedErrorType.ErrInvalidArg:
                return new DiscordEmbedBuilder()
                    .WithTitle("❌ Invalid argument.")
                    .WithColor(DiscordColor.Red)
                    .WithFooter("ERR_INVALID_ARG");
            default:
                return new DiscordEmbedBuilder()
                    .WithTitle("❌ An unknown error occurred.")
                    .WithColor(DiscordColor.Red)
                    .WithFooter("ERR_UNKNOWN");
        }
    }
    public static DiscordEmbedBuilder StatusEmbed(EmbedStatusType statusType)
    {
        switch (statusType)
        {
            case EmbedStatusType.StatusDisconnect:
                return new DiscordEmbedBuilder()
                    .WithTitle("👋 Bye bye!")
                    .WithDescription("Disconnected from the voice channel.")
                    .WithColor(DiscordColor.Green);
            case EmbedStatusType.StatusQueueEnd:
                return new DiscordEmbedBuilder()
                    .WithTitle("🏁 End of Queue")
                    .WithDescription("No more tracks to play.")
                    .WithColor(DiscordColor.Green);
            case EmbedStatusType.StatusInactivity:
                return new DiscordEmbedBuilder()
                    .WithTitle("⏱️ Disconnected due to inactivity.")
                    .WithColor(DiscordColor.Yellow);
            case EmbedStatusType.StatusClearQueue:
                return new DiscordEmbedBuilder()
                    .WithTitle("🧹 Queue Cleared")
                    .WithDescription("The queue has been cleared.")
                    .WithColor(DiscordColor.Green);
            default:
                return new DiscordEmbedBuilder()
                    .WithTitle("❔ Something happened.")
                    .WithDescription("An unknown status occurred.")
                    .WithColor(DiscordColor.Gray);
        }
    }
}