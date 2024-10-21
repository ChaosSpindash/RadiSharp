using DisCatSharp.Entities;

namespace RadiSharp.Typedefs
{
    /// <summary>
    /// Contains the imported YAML configuration.
    /// </summary>
    public class YamlConfig
    {
        /// <summary>
        /// The settings for the bot.
        /// </summary>
        public BotSettings BotSettings { get; set; } = new();

        /// <summary>
        /// The settings for Lavalink connections.
        /// </summary>
        public LavalinkSettings LavalinkSettings { get; set; } = new();
    }

    /// <summary>
    /// Contains various settings for the bot.
    /// </summary>
    public class BotSettings
    {
        /// <summary>
        /// The Discord bot token. This is required for the bot to function.
        /// </summary>
        public string Token { get; set; } = "";

        /// <summary>
        /// The activity settings for the bot.
        /// </summary>
        public BotActivity Activity { get; set; } = new();

        /// <summary>
        /// The guild ID to use for guild commands. If GlobalCommands is true, this is ignored.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// If true, the bot will use global commands. If false, the bot will use guild commands.
        /// </summary>
        public bool GlobalCommands { get; set; }
    }

    /// <summary>
    /// Contains the activity settings for the bot.
    /// </summary>
    public class BotActivity
    {
        /// <summary>
        /// The activity type of the bot shown to users. (Playing, Streaming, Listening, Watching, Custom)
        /// </summary>
        public ActivityType Type { get; set; }

        /// <summary>
        /// Additional information about the activity.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// The status of the bot shown to users. (Online, Idle, Do Not Disturb, Invisible)
        /// </summary>
        public UserStatus Status { get; set; }
    }

    /// <summary>
    /// Contains various settings for Lavalink connections.
    /// </summary>
    public class LavalinkSettings
    {
        /// <summary>
        /// The timeout in milliseconds before retrying to connect to a node.
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Contains the list of Lavalink nodes to connect to.
        /// </summary>
        public List<LavalinkNode> Nodes { get; set; } = new();
    }

    /// <summary>
    /// Contains the settings for a Lavalink node.
    /// </summary>
    public class LavalinkNode
    {
        /// <summary>
        /// The host name or IP address to connect to.
        /// </summary>
        public string Host { get; set; } = "";

        /// <summary>
        /// The port number to connect to.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// The password to authenticate with the node.
        /// </summary>
        public string Pass { get; set; } = "";

        /// <summary>
        /// If true, connect to the node via SSL.
        /// </summary>
        public bool Secure { get; set; } = false;
    }
}