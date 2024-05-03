using DisCatSharp.Entities;

namespace RadiSharp
{
    internal class Program
    {
        
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

            YamlConfig yamlConfig = yamlDeserializer.Deserialize<YamlConfig>(File.ReadAllText("config.yaml"));

            var Discord = new DiscordClient(new DiscordConfiguration()
            {
                Token = yamlConfig.Token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContent
            });

            await Discord.ConnectAsync(new DiscordActivity(yamlConfig.ActivityName,yamlConfig.ActivityType),yamlConfig.Status);
            await Task.Delay(-1);
        }
    }
}
