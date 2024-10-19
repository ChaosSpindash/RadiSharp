using DisCatSharp.Entities;

namespace RadiSharp
{
    public class YamlConfig
    {
        public BotSettings BotSettings { get; set; } = new();
        public List<LavalinkNode> LavalinkNodes { get; set; } = new();
    }

     public class BotSettings
    {
        public string Token { get; set; } = "";
        public ActivityType ActivityType { get; set; }
        public string ActivityName { get; set; } = "";
        public UserStatus Status { get; set; }
        public ulong GuildId { get; set; }
    }

    public class LavalinkNode
    {
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string Pass { get; set; } = "";
        public bool Secure { get; set; } = false;
    }
}
