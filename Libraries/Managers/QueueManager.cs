using System.Text;

namespace RadiSharp.Libraries.Managers;

/// <summary>
/// Manages the queue for the Lavalink player.
/// </summary>
public class QueueManager
{
    /// <summary>
    /// The current instance of <c>QueueManager</c>. Only one instance can exist at a time.
    /// </summary>
    public static QueueManager Instance { get; } = new();

    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    /// <summary>
    /// Contains the queued tracks in sequential order.
    /// </summary>
    private List<RadiTrack> _playlist = [];
    /// <summary>
    /// Contains the queued tracks in shuffled order.
    /// </summary>
    private List<RadiTrack> _shuffledPlaylist = [];
    /// <summary>
    /// The index of the current track in the playlist.
    /// </summary>
    private int _playlistIndex = -1;
    /// <summary>
    /// If true, the queue will loop when it reaches the end.
    /// </summary>
    public bool LoopQueue { get; private set; }
    /// <summary>
    /// If true, the queue is shuffled.
    /// </summary>
    public bool Shuffle { get; private set; }
    /// <summary>
    /// If true, the current track will loop.
    /// </summary>
    public bool Loop { get; private set; }
    /// <summary>
    /// Defines the current state of the player.
    /// </summary>
    public bool IsPlaying { get; set; }
    /// <summary>
    /// The index of the current page in the playlist.
    /// </summary>
    public int PageCurrent { get; set; }
    /// <summary>
    /// The total number of pages in the playlist. One page contains up to 10 tracks.
    /// </summary>
    public int PageCount { get; private set; }
    
    /// <summary>
    /// Adds a track to the queue.
    /// </summary>
    /// <param name="track">The track to add to the queue.</param>
    public void Add(RadiTrack track)
    {
        _playlist.Add(track);
        // If shuffled playlist is not empty, add the track to it
        if (Shuffle)
        {
            _shuffledPlaylist.Add(track);
        }
    }
    /// <summary>
    /// Adds a playlist to the queue.
    /// </summary>
    /// <param name="playlist">The playlist to add to the queue.</param>
    public void AddPlaylist(RadiPlaylist playlist)
    {
        foreach (RadiTrack track in playlist.Tracks)
        {
            Add(track);
        }
    }
    
    /// <summary>
    /// Removes a track from the queue.
    /// </summary>
    /// <param name="index">The index of the track to remove from the queue.</param>
    public void Remove(int index)
    {
        List<RadiTrack> playlist;
        // First check if the shuffle setting is enabled
        if (Shuffle)
        {
            // Determine which playlist is currently in use
            playlist = Shuffle ? _shuffledPlaylist : _playlist;
            List<RadiTrack> otherPlaylist = Shuffle ? _playlist : _shuffledPlaylist;

            // Check if the index is out of bounds
            if (index < 1 || index > playlist.Count)
            {
                return;
            }
            // Remove the track from other playlist first
            otherPlaylist.Remove(playlist[index - 1]);
        }
        playlist = _playlist;
        // Then remove the track from the current playlist
        playlist.RemoveAt(index - 1);

        // If the index is the current track, skip to the next track
        if (index == _playlistIndex)
        {
            _playlistIndex--;
            Next(true);
        }
    }
    /// <summary>
    /// Clears the queue.
    /// </summary>
    public void Clear()
    {
        // Clear the playlist (including shuffle playlist) and reset the index
        _playlistIndex = -1;
        _playlist.Clear();
        _shuffledPlaylist.Clear();
        // Reset the queue settings
        LoopQueue = false;
        Shuffle = false;
        Loop = false;
        IsPlaying = false;
    }
    /// <summary>
    /// Returns the next track in the queue.
    /// </summary>
    /// <param name="skip">Determines whether the track was skipped manually.</param>
    /// <returns>
    /// <list type="bullet">
    /// <item>The next track to play in the queue.</item>
    /// <item><c>null</c>, if the queue is empty OR has reached the end and <c>LoopQueue</c> is false.</item>
    /// </list>
    /// </returns>
    public RadiTrack? Next(bool skip = false)
    {
        // Determine which playlist to use based on the shuffle setting
        List<RadiTrack> playlist = Shuffle ? _shuffledPlaylist : _playlist;

        // If the playlist is empty, return null
        if (playlist.Count == 0)
        {
            return null;
        }
        // If Repeat is enabled, return the current track
        if (Loop && !skip)
        {
            return playlist[_playlistIndex];
        }
        // Increment the playlist index and check if it's out of bounds
        _playlistIndex++;
        if (_playlistIndex >= playlist.Count)
        {
            // If the queue is set to loop, reset the index to 0
            if (LoopQueue)
            {
                _playlistIndex = 0;
            }
            // If the queue is not set to loop, clear it and return null
            else
            {
                Clear();
                return null;
            }
        }
        // Return the next track
        return playlist[_playlistIndex];
    }
    
    /// <summary>
    /// Skips to the specified track in the queue.
    /// </summary>
    /// <param name="index">The index of the track to skip to.</param>
    /// <returns>The track to skip to.</returns>
    public RadiTrack? SkipTo(int index)
    {
        // Determine which playlist to use based on the shuffle setting
        List<RadiTrack> playlist = Shuffle ? _shuffledPlaylist : _playlist;
        // Check if the index is out of bounds
        if (index < 1 || index > playlist.Count)
        {
            return null;
        }
        // Set the playlist index to the new index
        _playlistIndex = index - 1;
        // Return the new track
        return playlist[_playlistIndex];
    }
    /// <summary>
    /// Moves a track in the queue from one index to another.
    /// </summary>
    /// <param name="from">The index of the track to move.</param>
    /// <param name="to">The index to move the track to.</param>
    public void Move(int from, int to)
    {
        // First check if the shuffle setting is enabled
        List<RadiTrack> playlist = Shuffle ? _shuffledPlaylist : _playlist;

        // Check if the indices are out of bounds
        if (from < 1 || from > playlist.Count || to < 1 || to > playlist.Count)
        {
            return;
        }

        // Then move the track in the current playlist
        playlist.Insert(to - 1, playlist[from - 1]);
        if (from < to)
        {
            playlist.RemoveAt(from - 1);
        }
        else
        {
            playlist.RemoveAt(from);
        }
        // Update the playlist index if necessary
        if (_playlistIndex == from - 1)
        {
            _playlistIndex = to - 1;
        }
        else if (_playlistIndex == to - 1 && from < to)
        {
            _playlistIndex--;
        }
        else if (_playlistIndex == to && from > to)
        {
            _playlistIndex++;
        }
    }
    
    /// <summary>
    /// Returns the previous track in the queue.
    /// </summary>
    /// <returns>The track before the current playlist index.</returns>
    public RadiTrack? Previous()
    {
        // Determine which playlist to use based on the shuffle setting
        List<RadiTrack> playlist = Shuffle ? _shuffledPlaylist : _playlist;
        // If the playlist is empty, return null
        if (playlist.Count == 0)
        {
            return null;
        }
        // Decrement the playlist index
        _playlistIndex--;
        // If the index is out of bounds, set it to the first track
        if (_playlistIndex < 0)
        {
            _playlistIndex = 0;
        }
        // Return the previous track
        return playlist[_playlistIndex];
    }
    
    /// <summary>
    /// Toggles the Loop Queue setting.
    /// </summary>
    public void ToggleLoopQueue() => LoopQueue = !LoopQueue;
    /// <summary>
    /// Toggles the Shuffle setting.
    /// </summary>
    public void ToggleShuffle()
    {
        Shuffle = !Shuffle;
        if (Shuffle)
        {
            // Place the current track at the beginning of the shuffled playlist and shuffle the rest
            RadiTrack currentTrack = _playlist[_playlistIndex];
            _shuffledPlaylist = _playlist.ToList();
            _shuffledPlaylist.RemoveAt(_playlistIndex);
            _shuffledPlaylist = _shuffledPlaylist.OrderBy(_ => Guid.NewGuid()).ToList();
            _shuffledPlaylist.Insert(0, currentTrack);
            _playlistIndex = 0;
        }
        else
        {
            _playlistIndex = _playlist.IndexOf(_shuffledPlaylist[_playlistIndex]);
            _shuffledPlaylist.Clear();
        }
    }
    /// <summary>
    /// Toggles the Loop setting.
    /// </summary>
    public void ToggleLoop() => Loop = !Loop;
    
    /// <summary>
    /// Returns the current track in the queue.
    /// </summary>
    /// <returns>The current track.</returns>
    public RadiTrack? CurrentTrack()
    {
        // If the playlist index is out of bounds, return null
        if (_playlistIndex < 0 || _playlistIndex >= _playlist.Count)
        {
            return null;
        }
        // Determine which playlist to use based on the shuffle setting
        List<RadiTrack> playlist = Shuffle ? _shuffledPlaylist : _playlist;
        // Return the current track
        return playlist[_playlistIndex];
    }
    
    /// <summary>
    /// Returns the track at the specified index in the queue.
    /// </summary>
    /// <param name="index">The index of the track.</param>
    /// <returns>The track at the selected index.</returns>
    public RadiTrack? GetTrack(int index)
    {
        // Determine which playlist to use based on the shuffle setting
        List<RadiTrack> playlist = Shuffle ? _shuffledPlaylist : _playlist;
        // Check if the index is out of bounds
        if (index < 1 || index > playlist.Count)
        {
            return null;
        }
        // Return the track at the specified index
        return playlist[index - 1];
    }
    
    /// <summary>
    /// Returns the number of tracks in the queue.
    /// </summary>
    /// <returns>The total number of tracks in the queue.</returns>
    public int PlaylistCount() => _playlist.Count;
    
    /// <summary>
    /// Returns the queue in a paginated format.
    /// </summary>
    /// <param name="page">The page to return. If 0, selects the page containing the current track.</param>
    /// <returns>The formatted content of the selected queue page.</returns>
    /// <remarks>
    /// Each page contains up to 10 tracks. The text is formatted with the current track in bold.
    /// Along with the title, each track includes the author, duration, and the user who requested it.
    /// The text is intended to be used as a description in an embed. 
    /// </remarks>
    public string GetPlaylist(int page = 0)
    {
        // Determine which playlist to use based on the shuffle setting
        List<RadiTrack> playlist = Shuffle ? _shuffledPlaylist : _playlist;
        // Calculate the total number of pages
        PageCount = (int)Math.Ceiling((double)playlist.Count / 10);
        // Check if the page is out of bounds
        if (page < 0 || page > PageCount)
        {
            return "Invalid page number.";
        }
        // If page is 0, set it to the page containing the current track
        if (page == 0)
        {
            page = (int)Math.Ceiling((double)(_playlistIndex + 1) / 10);
        }
        // Set the current page
        PageCurrent = page;
        // Initialize the StringBuilder
        StringBuilder sb = new();
        // Loop through the tracks in the playlist and add them to the StringBuilder
        for (int i = (page - 1) * 10; i < page * 10 && i < playlist.Count; i++)
        {
            RadiTrack track = playlist[i];
            // Check if the track is the current track
            if (i == _playlistIndex)
            {
                sb.Append($"**`{i + 1}.` [{track.Track.Info.Title}]({track.Track.Info.Uri}) - {track.Track.Info.Author}** (`{FormatDuration(track.Track.Info.Length)}`) - {track.RequestedBy.Mention}\n");
            }
            else
            {
                sb.Append($"`{i + 1}.` [{track.Track.Info.Title}]({track.Track.Info.Uri}) - {track.Track.Info.Author} (`{FormatDuration(track.Track.Info.Length)}`) - {track.RequestedBy.Mention}\n");
            }
        }
        // Return the formatted playlist
        return sb.ToString();
    }

    /// <summary>
    /// Returns the total duration of the queue.
    /// </summary>
    /// <returns>The total duration of all tracks in the queue.</returns>
    public TimeSpan PlaylistDuration()
    {
        // Determine which playlist to use based on the shuffle setting
        List<RadiTrack> playlist = Shuffle ? _shuffledPlaylist : _playlist;
        // Initialize the TimeSpan
        TimeSpan duration = new();
        // Loop through the tracks in the playlist and add the duration to the TimeSpan
        foreach (RadiTrack track in playlist)
        {
            duration += track.Track.Info.Length;
        }
        // Return the total duration
        return duration;
    }
    
    /// <summary>
    /// Returns the formatted duration of a track. Leading zeros are omitted.
    /// </summary>
    /// <param name="duration">The track duration to be formatted.</param>
    /// <returns>The formatted duration.</returns>
    public static string FormatDuration(TimeSpan duration)
    {
        // If the duration is less than 1 hour, return the minutes and seconds
        // Otherwise, return the hours, minutes, and seconds
        return duration.ToString(duration.Hours == 0 ? @"m\:ss" : @"h\:mm\:ss");
    }
}