using DisCatSharp.Entities;
using DisCatSharp.Lavalink.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadiSharp.Libraries
{
    public class RadiTrack
    {
        public LavalinkTrack Track { get; set; }
        public DiscordUser RequestedBy { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public RadiTrack(LavalinkTrack track, DiscordUser requestedBy)
        {
            Track = track;
            RequestedBy = requestedBy;
        }
    }
}
