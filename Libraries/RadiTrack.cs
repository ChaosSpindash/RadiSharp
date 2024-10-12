using DisCatSharp.Entities;
using DisCatSharp.Lavalink.Entities;

namespace RadiSharp.Libraries
{
    public class RadiTrack(LavalinkTrack track, DiscordUser requestedBy)
    {
        public LavalinkTrack Track { get; set; } = track;
        public DiscordUser RequestedBy { get; set; } = requestedBy;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
