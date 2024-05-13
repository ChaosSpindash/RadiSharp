using System.Text;

namespace RadiSharp.Libraries
{
    public class QueueManager
    {
        private static readonly QueueManager _instance = new QueueManager();
        public static QueueManager Instance
        {
            get
            {
                return _instance;
            }
        }

        private List<RadiTrack> _playlist = [];
        private List<RadiTrack> _shuffledPlaylist = [];
        private int _playlistIndex = -1;
        public bool LoopQueue { get; private set; } = false;
        public bool Shuffle { get; private set; } = false;
        public bool Repeat { get; private set; } = false;

        public int PageCurrent { get; private set; } = 0;
        public int PageCount { get; private set; } = 0;

        public void Add(RadiTrack track)
        {
            _playlist.Add(track);
            // If shuffled playlist is not empty, add the track to it
            if (Shuffle)
            {
                _shuffledPlaylist.Add(track);
            }
        }

        public void AddPlaylist(RadiPlaylist playlist)
        {
            foreach (RadiTrack track in playlist.Tracks)
            {
                Add(track);
            }
        }

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
        public void Clear()
        {
            // Clear the playlist (including shuffle playlist) and reset the index
            _playlistIndex = -1;
            _playlist.Clear();
            _shuffledPlaylist.Clear();
            // Reset the queue settings
            LoopQueue = false;
            Shuffle = false;
            Repeat = false;
        }
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
            if (Repeat && !skip)
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

        public void Move(int from, int to)
        {
            List<RadiTrack> playlist;
            // First check if the shuffle setting is enabled
            playlist = Shuffle ? _shuffledPlaylist : _playlist;

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

        public void ToggleLoopQueue() => LoopQueue = !LoopQueue;
        public void ToggleShuffle()
        {
            Shuffle = !Shuffle;
            if (Shuffle)
            {
                // Place the current track at the beginning of the shuffled playlist and shuffle the rest
                RadiTrack currentTrack = _playlist[_playlistIndex];
                _shuffledPlaylist = _playlist.ToList();
                _shuffledPlaylist.RemoveAt(_playlistIndex);
                _shuffledPlaylist = _shuffledPlaylist.OrderBy(x => Guid.NewGuid()).ToList();
                _shuffledPlaylist.Insert(0, currentTrack);
                _playlistIndex = 0;
            }
            else
            {
                _playlistIndex = _shuffledPlaylist.IndexOf(_playlist[_playlistIndex]);
                _shuffledPlaylist.Clear();
            }
        }
        public void ToggleRepeat() => Repeat = !Repeat;

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

        public int PlaylistCount() => _playlist.Count;

        // Method to return the queue in a paginated format (10 tracks per page)
        // Includes the track duration and the user who requested the track
        // Mark the current track as bold
        public string GetPlaylist(int page = 0)
        {
            // Determine which playlist to use based on the shuffle setting
            List<RadiTrack> playlist = Shuffle ? _shuffledPlaylist : _playlist;
            // Calculate the total number of pages
            PageCount = (int)Math.Ceiling((double)playlist.Count / 10);
            // Check if the page is out of bounds
            if (page < 0 || page >= PageCount)
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
                    sb.Append($"**`{i + 1}.` {track.Track.Info.Title}** (`{FormatDuration(track.Track.Info.Length)}`) - {track.RequestedBy.Mention}\n");
                }
                else
                {
                    sb.Append($"`{i + 1}.` {track.Track.Info.Title} (`{FormatDuration(track.Track.Info.Length)}`) - {track.RequestedBy.Mention}\n");
                }
            }
            // Return the formatted playlist
            return sb.ToString();
        }

        // Method to return the total duration of the playlist
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
        
        public static string FormatDuration(TimeSpan duration)
        {
            // If the duration is less than 1 hour, return the minutes and seconds
            if (duration.Hours == 0)
            {
                return duration.ToString(@"m\:ss");
            }
            // Otherwise, return the hours, minutes, and seconds
            return duration.ToString(@"h\:mm\:ss");
        }
    }
}
