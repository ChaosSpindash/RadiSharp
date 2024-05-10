using DisCatSharp.Entities;
using DisCatSharp.Lavalink.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadiSharp.Libraries
{
    public class RadiPlaylist
    {
        public List<RadiTrack> Tracks { get; set; } = [];
        
        public RadiPlaylist(LavalinkPlaylist playlist, DiscordUser requestedBy)
        {
            foreach (LavalinkTrack track in playlist.Tracks)
            {
                Tracks.Add(new RadiTrack(track, requestedBy));
            }
        }
    }
}
