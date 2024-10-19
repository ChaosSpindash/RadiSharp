using DisCatSharp.Entities;
using DisCatSharp.Lavalink.Entities;

namespace RadiSharp.Libraries
{
    public class RadiPlaylist
    {
        public List<RadiTrack> Tracks { get; } = [];
        
        public RadiPlaylist(LavalinkPlaylist playlist, DiscordUser requestedBy)
        {
            foreach (LavalinkTrack track in playlist.Tracks)
            {
                Tracks.Add(new RadiTrack(track, requestedBy));
            }
        }
    }
}
