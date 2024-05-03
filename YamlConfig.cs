using DisCatSharp.Entities;

namespace RadiSharp
{
    class YamlConfig
    {
        public string Token { get; set; }
        public ActivityType ActivityType { get; set; }
        public string ActivityName { get; set; }
        public UserStatus Status { get; set; }
    }
}
