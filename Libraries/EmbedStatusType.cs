namespace RadiSharp.Libraries;

public enum EmbedStatusType
{
    StatusUnknown,                  // Unknown status
    StatusIdle,                     // Idle status
    StatusPlaying,                  // Playing status
    StatusPaused,                   // Paused status
    StatusStopped,                  // Stopped status
    StatusQueueEnd,                 // Queue end status
    StatusLoopTrack,                // Loop track status
    StatusLoopQueue,                // Loop queue status
    StatusLoopDisabled,             // Loop disabled status
    StatusShuffle,                  // Shuffle status
    StatusShuffleDisabled,          // Shuffle disabled status
    StatusAddTrack,                 // Add track status
    StatusAddPlaylist,              // Add playlist status
    StatusRemoveTrack,              // Remove track status
    StatusClearQueue,               // Clear queue status
    StatusSkipTrack,                // Skip track status
    StatusSkipToTrack,              // Skip to track status
    StatusPreviousTrack,            // Previous track status
    StatusMoveTrack,                // Move track status
    StatusDisconnect,               // Disconnect status
    StatusInactivity,               // Disconnect by inactivity status
}