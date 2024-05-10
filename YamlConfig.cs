using DisCatSharp.Entities;

namespace RadiSharp
{
    class YamlConfig
    {
        public string Token { get; set; } = "";
        public ActivityType ActivityType { get; set; }
        public string ActivityName { get; set; } = "";
        public UserStatus Status { get; set; }
        public ulong GuildId { get; set; }
        public string LavalinkHost { get; set; } = "";
        public int LavalinkPort { get; set; }
        public string LavalinkPass { get; set; } = "";
    }
}
