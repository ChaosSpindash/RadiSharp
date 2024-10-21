using DisCatSharp.ApplicationCommands.Context;
using Serilog;

namespace RadiSharp.Libraries.Managers;

/// <summary>
/// Logs various bot events. Uses the globally available logger.
/// </summary>
public static class EventLogger
{
    /// <summary>
    /// Logs a specified player event.
    /// </summary>
    /// <param name="guildId">The ID of the guild the event is triggered from.</param>
    /// <param name="type">The type of the player event.</param>
    /// <param name="args">An array of additional arguments. Required for certain log types, specifically errors.</param>
    public static void LogPlayerEvent(ulong guildId, PlayerEventType type, string[]? args = null)
    {
        switch (type)
        { 
            case PlayerEventType.Connect:
                Log.Logger.Information($"Guild {guildId} | Connected to voice channel: {args![0]}");
                break;
            case PlayerEventType.Disconnect:
                Log.Logger.Information($"Guild {guildId} | Disconnected from voice channel");
                break;
            case PlayerEventType.Play:
                Log.Logger.Information($"Guild {guildId} | Started playback: {args![0]}");
                break;
            case PlayerEventType.Pause:
                Log.Logger.Information($"Guild {guildId} | Paused playback");
                break;
            case PlayerEventType.Resume:
                Log.Logger.Information($"Guild {guildId} | Resumed playback");
                break;
            case PlayerEventType.Stop:
                Log.Logger.Information($"Guild {guildId} | Stopped playback");
                break;
            case PlayerEventType.Queue:
                Log.Logger.Information($"Guild {guildId} | Queued track: {args![0]}");
                break;
            case PlayerEventType.QueuePlaylist:
                Log.Logger.Information($"Guild {guildId} | Queued playlist: {args![0]}");
                break;
            case PlayerEventType.Clear:
                Log.Logger.Information($"Guild {guildId} | Cleared queue");
                break;
            case PlayerEventType.Loop:
                Log.Logger.Information($"Guild {guildId} | Loop {args![0]}");
                break;
            case PlayerEventType.LoopQueue:
                Log.Logger.Information($"Guild {guildId} | Loop Queue {args![0]}");
                break;
            case PlayerEventType.Shuffle:
                Log.Logger.Information($"Guild {guildId} | Shuffle {args![0]}");
                break;
            case PlayerEventType.Remove:
                Log.Logger.Information($"Guild {guildId} | Removed track {args![0]}");
                break;
            case PlayerEventType.Move:
                Log.Logger.Information($"Guild {guildId} | Moved track {args![0]} -> {args[1]}");
                break;
            case PlayerEventType.Search:
                Log.Logger.Information($"Guild {guildId} | Searched for \"{args![0]}\", found {args[1]} results");
                break;
            case PlayerEventType.Error:
                Log.Logger.Error($"Guild {guildId} | {args![0]}: {args[1]}");
                break;
        }
    }
}

/// <summary>
/// Contains the types of player events that can be logged.
/// </summary>
public enum PlayerEventType
{
    Connect,
    Disconnect,
    Play,
    Pause,
    Resume,
    Stop,
    Queue,
    QueuePlaylist,
    Clear,
    Loop,
    LoopQueue,
    Shuffle,
    Remove,
    Move,
    Search,
    Error
}