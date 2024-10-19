namespace RadiSharp.Typedefs;

public enum EmbedErrorType
{
    ErrUnknown,                     // Unknown error
    ErrPermission,                  // Permission error
    ErrNotFound,                    // Not found error
    ErrInvalidArg,                  // Invalid argument error
    ErrInvalidCmd,                  // Invalid command error
    ErrIndexOutOfRange,             // Index out of range error
    ErrRequestTimeout,              // Request timeout error
    ErrUserNotInVoice,              // User not in voice error
    ErrVoiceChannelInvalid,         // Voice channel invalid error
    ErrLavalinkConn,                // Lavalink connection error
    ErrLavalinkSearch,              // Lavalink search error
    ErrLavalinkAccessDenied,        // Lavalink Access Denied (403) error
    ErrLavalinkLoadFailed,          // Lavalink load failed error
    ErrLavalinkStuck,               // Lavalink track stuck error
    ErrLavalinkNoSession,           // Lavalink no session error
    ErrLavalinkNoMatches,           // Lavalink no matches error
    ErrLavalinkNoTracks,            // Lavalink no tracks error
    ErrLavalinkNoPlaylist,          // Lavalink no playlist error
    ErrLavalinkQueueEmpty,          // Lavalink queue empty error
    ErrLavalinkPotoken,             // Lavalink Proof-of-Origin token error
    ErrLavalinkOauth,               // Lavalink OAuth error
    ErrLavalinkNoGuild,             // Lavalink no guild error
    ErrLavalinkAgeRestricted,       // Lavalink age restricted error
}