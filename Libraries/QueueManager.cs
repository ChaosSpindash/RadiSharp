using DisCatSharp.Lavalink.Entities;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadiSharp.Libraries
{
    public class QueueManager
    {
        
        private List<RadiTrack> _playlist = [];
        private List<RadiTrack> _shuffledPlaylist = [];
        private int _playlistIndex = -1;
        public bool LoopQueue { get; private set; } = false;
        public bool Shuffle { get; private set; } = false;
        public bool Repeat { get; private set; } = false;

        // Returns whether the queue was manually cleared
        public bool ManualClear { get; private set; } = false;

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
        public void Clear(bool manual = false)
        {
            // Clear the playlist (including shuffle playlist) and reset the index
            _playlistIndex = -1;
            _playlist.Clear();
            _shuffledPlaylist.Clear();
            // Reset the queue settings
            LoopQueue = false;
            Shuffle = false;
            Repeat = false;
            ManualClear = manual;
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
                _playlistIndex = -1;
                _shuffledPlaylist = _playlist.OrderBy(x => Guid.NewGuid()).ToList();
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

        // Method to return the current playlist in pages of 10
        // Includes the track duration and the user who requested the track
        // If page contains current track, mark line as bold
        public string GetPlaylist(int page = 0)
        {
            // Determine which playlist to use based on the shuffle setting
            List<RadiTrack> playlist = Shuffle ? _shuffledPlaylist : _playlist;
            // Calculate the total number of pages
            int totalPages = (int)Math.Ceiling((double)playlist.Count / 10);
            PageCount = totalPages;
            // If the page number is out of bounds, clamp to the nearest valid page
            if (page < 0)
            {
                page = 1;
            }
            else if (page > totalPages)
            {
                page = totalPages;
            }
            // If page number is 0, return page with current track
            if (page == 0)
            {
                page = (int)Math.Ceiling((double)(_playlistIndex + 1) / 10);
            }
            // Calculate the starting index and ending index of the current page
            PageCurrent = page;
            int start = (page - 1) * 10;
            int end = Math.Min(start + 10, playlist.Count);
            // Initialize the StringBuilder
            StringBuilder sb = new();
            // Loop through the tracks in the current page
            for (int i = start; i < end; i++)
            {
                // If the track is the current track, mark the line as bold
                if (i == _playlistIndex)
                {
                    sb.Append($"**`{i + 1}.` [{playlist[i].Track.Info.Title}]({playlist[i].Track.Info.Uri})** - `{playlist[i].Track.Info.Length}` [{playlist[i].RequestedBy.Mention}]\n");
                }
                else
                {
                    sb.Append($"`{i + 1}.` [{playlist[i].Track.Info.Title}]({playlist[i].Track.Info.Uri}) - `{playlist[i].Track.Info.Length}` [{playlist[i].RequestedBy.Mention}]\n");
                }
            }
            // Return the StringBuilder as a string
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
        
    }
}
