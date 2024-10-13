using DisCatSharp.Entities;
using DisCatSharp.Lavalink;
using DisCatSharp.Lavalink.Entities;

namespace RadiSharp.Libraries;

public static class EmbedGenerator
{
    // ErrorEmbed and StatusEmbed are for commonly used embeds that do not require additional information.
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
                    .WithDescription("The track may be unavailable or age-restricted.\nIf this issue persists, contact the bot maintainer.")
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
            case EmbedErrorType.ErrVoiceChannelInvalid:
                return new DiscordEmbedBuilder()
                    .WithTitle("❌ Invalid voice channel.")
                    .WithDescription("Please join a valid voice or stage channel.")
                    .WithColor(DiscordColor.Red)
                    .WithFooter("ERR_VOICE_CHANNEL_INVALID");
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
                    .WithDescription("Playback has now stopped.")
                    .WithColor(DiscordColor.Green);
            default:
                return new DiscordEmbedBuilder()
                    .WithTitle("❔ Something happened.")
                    .WithDescription("An unknown status occurred.")
                    .WithColor(DiscordColor.Gray);
        }
    }
    // Embeds that require additional information from commands must be generated individually.
    // Therefore, the following methods are tailored to the specific commands that require them.
    public static DiscordEmbedBuilder PlayerEmbed(QueueManager queueManager, bool pause = false)
    {
        DiscordEmbedBuilder embed = new();
        var track = queueManager.CurrentTrack()!;

        if (!pause)
            embed.WithTitle("▶️ Now Playing").WithColor(DiscordColor.Green);
        else
            embed.WithTitle("⏸️ Paused").WithColor(DiscordColor.Yellow);
        
        return embed
                .WithDescription($"[{track.Track.Info.Title}]({track.Track.Info.Uri})")
                .WithThumbnail($"https://img.youtube.com/vi/{track.Track.Info.Identifier}/maxresdefault.jpg")
                .AddFields(
                    [
                        new DiscordEmbedField("Requested by", track.RequestedBy.Mention, true),
                        new DiscordEmbedField("Duration", track.Track.Info.IsStream ? "`🔴 LIVE`" : $"`{QueueManager.FormatDuration(track.Track.Info.Length)}`", true)
                    ]
                );
    }
    public static DiscordComponent[] PlayerComponents(QueueManager queueManager, bool pause = false)
    {
        var components = new List<DiscordComponent>
        {
            new DiscordButtonComponent(ButtonStyle.Primary, "player_previous", "", false, new DiscordComponentEmoji("⏮️")),
            new DiscordButtonComponent(ButtonStyle.Primary, "player_pause", "", false, new DiscordComponentEmoji("⏸️")),
            new DiscordButtonComponent(ButtonStyle.Primary, "player_skip", "", false, new DiscordComponentEmoji("⏭️")),
            new DiscordButtonComponent(ButtonStyle.Primary, "player_stop", "", false, new DiscordComponentEmoji("⏹️")),
            new DiscordButtonComponent(queueManager.Loop ? ButtonStyle.Success : ButtonStyle.Secondary, "player_loop", "", false, new DiscordComponentEmoji("🔂"))
        };
        if (pause)
            components[1] = new DiscordButtonComponent(ButtonStyle.Primary, "player_resume", "", false, new DiscordComponentEmoji("▶️"));
        return components.ToArray();
    }

    public static DiscordEmbedBuilder QueueEmbed(QueueManager queueManager, LavalinkGuildPlayer guildPlayer, int pageJump = 0)
    {
        DiscordEmbedBuilder embed = new();
        var description = pageJump != 0 ? queueManager.GetPlaylist(queueManager.PageCurrent + pageJump) : queueManager.GetPlaylist();

        return embed
            .WithTitle("📜 Queue")
            .WithDescription(description)
            .WithColor(DiscordColor.Blurple)
            .AddFields(
            [
                new DiscordEmbedField("Total Tracks", $"`{queueManager.PlaylistCount()}`", true),
                new DiscordEmbedField("Current Track Position",
                    $"`{QueueManager.FormatDuration(guildPlayer.CurrentTrack!.Info.Position)} / {QueueManager.FormatDuration(queueManager.CurrentTrack()!.Track.Info.Length)}`",
                    true),
                new DiscordEmbedField("Total Duration",
                    $"`{QueueManager.FormatDuration(queueManager.PlaylistDuration())}`", true)
            ])
            .WithFooter($"Page {queueManager.PageCurrent}/{queueManager.PageCount}");
    }
    public static DiscordComponent[] QueueComponents(QueueManager queueManager)
    {
        var components = new List<DiscordComponent>
        {
            new DiscordButtonComponent(ButtonStyle.Primary, "queue_previous_page", "", false, new DiscordComponentEmoji("◀️")),
            new DiscordButtonComponent(ButtonStyle.Primary, "queue_next_page", "", false, new DiscordComponentEmoji("▶️")),
            new DiscordButtonComponent(queueManager.LoopQueue ? ButtonStyle.Success : ButtonStyle.Secondary, "queue_loop", "", false, new DiscordComponentEmoji("🔁")),
            new DiscordButtonComponent(queueManager.Shuffle ? ButtonStyle.Success : ButtonStyle.Secondary, "queue_shuffle", "", false, new DiscordComponentEmoji("🔀")),
            new DiscordButtonComponent(ButtonStyle.Danger, "queue_clear", "", false, new DiscordComponentEmoji("🗑️"))
        };
        return components.ToArray();
    }

    public static DiscordEmbedBuilder AddTrackEmbed(RadiTrack track)
    {
        return new DiscordEmbedBuilder()
            .WithTitle("📝 Added to Queue")
            .WithDescription($"[{track.Track.Info.Title}]({track.Track.Info.Uri})")
            .WithThumbnail($"https://img.youtube.com/vi/{track.Track.Info.Identifier}/maxresdefault.jpg")
            .WithColor(DiscordColor.Green)
            .AddFields(
            [
                new DiscordEmbedField("Requested by", track.RequestedBy.Mention, true),
                new DiscordEmbedField("Duration",
                    track.Track.Info.IsStream ? "`🔴 LIVE`" : $"`{FormatDuration(track.Track.Info.Length)}`", true)
            ]);
    }
    public static DiscordEmbedBuilder AddPlaylistEmbed(LavalinkPlaylist playlist, string requestedBy)
    {
        return new DiscordEmbedBuilder()
            .WithTitle("📝 Added Playlist to Queue")
            .WithDescription($"[{playlist.Info.Name}]({playlist.Tracks.First().Info.Uri})")
            .WithThumbnail($"https://img.youtube.com/vi/{playlist.Tracks.First().Info.Identifier}/maxresdefault.jpg")
            .WithColor(DiscordColor.Green)
            .AddFields(
            [
                new DiscordEmbedField("Requested by", requestedBy, true),
                new DiscordEmbedField("Tracks", $"`{playlist.Tracks.Count}`", true)
            ]);
    }

    public static DiscordEmbedBuilder RemoveTrackEmbed(RadiTrack track)
    {
        return new DiscordEmbedBuilder()
            .WithTitle("🗑️ Removed from Queue")
            .WithDescription($"[{track.Track.Info.Title}]({track.Track.Info.Uri})")
            .WithThumbnail($"https://img.youtube.com/vi/{track.Track.Info.Identifier}/maxresdefault.jpg")
            .WithColor(DiscordColor.Green)
            .AddFields(
            [
                new DiscordEmbedField("Requested by", track.RequestedBy.Mention, true),
                new DiscordEmbedField("Duration", track.Track.Info.IsStream ? "`🔴 LIVE`" : $"`{track.Track.Info.Length}`", true)
            ]);
    }
    
    public static DiscordEmbedBuilder NoMatchErrorEmbed(string query)
    {
        return new DiscordEmbedBuilder()
            .WithTitle("❌ No match found.")
            .WithDescription($"No tracks or playlists found for `{query}`.")
            .WithColor(DiscordColor.Red)
            .WithFooter("ERR_LAVALINK_QUERY_NO_MATCH");
    }

    private static string FormatDuration(TimeSpan duration)
    {
        // If the duration is less than 1 hour, return the minutes and seconds
        // Otherwise, return the hours, minutes, and seconds
        return duration.ToString(duration.Hours == 0 ? @"m\:ss" : @"h\:mm\:ss");
    }
}