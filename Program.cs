using DisCatSharp.ApplicationCommands;
using DisCatSharp.Entities;
using DisCatSharp.Interactivity;
using DisCatSharp.Interactivity.Extensions;
using DisCatSharp.Net;
using DisCatSharp.Lavalink;
using RadiSharp.Commands;
using Microsoft.Extensions.Logging;
using RadiSharp.Libraries;
using Serilog;

namespace RadiSharp
{
    internal static class Program
    {

        static void Main()
        {
            // Start the main bot loop
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            // Initialize the logger
            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
                .WriteTo.Console()
                .WriteTo.File($"logs/radi.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // Initialize the logger factory
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddSerilog();
            });

            // Deserialize the config file
            var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

            YamlConfig yamlConfig;
            try
            {
#if DEBUG
                yamlConfig = yamlDeserializer.Deserialize<YamlConfig>(await File.ReadAllTextAsync("config.canary.yaml"));
#else
                yamlConfig = yamlDeserializer.Deserialize<YamlConfig>(await File.ReadAllTextAsync("config.yaml"));
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                Log.Logger.Fatal($"Could not load config.canary.yaml - Abort.\n{ex}");
#else
                Log.Logger.Fatal($"Could not load config.yaml - Abort.\n{ex}");
#endif
                await Task.Delay(5000);
                Environment.ExitCode = 2;
                return;
            }
            Log.Logger.Information("config.yaml successfully loaded.");

            // Initialize the bot
            var discord = new DiscordClient(new DiscordConfiguration()
            {
                Token = yamlConfig.Token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContent,
                LoggerFactory = loggerFactory
            });

            discord.UseInteractivity(new InteractivityConfiguration
            {
                Timeout = TimeSpan.FromSeconds(30)
            });

            // Initialize the Lavalink config
            var endpoint = new ConnectionEndpoint(yamlConfig.LavalinkHost, yamlConfig.LavalinkPort, yamlConfig.LavalinkSsl);
            var lavalinkConfig = new LavalinkConfiguration
            {
                Password = yamlConfig.LavalinkPass,
                RestEndpoint = endpoint,
                SocketEndpoint = endpoint
            };

            var lavalink = discord.UseLavalink();

            // Register the slash commands
            // NOTE: Global commands only update every hour, so it's recommended to use guild commands during development
            var appCommands = discord.UseApplicationCommands();

            appCommands.RegisterGuildCommands<RadiSlashCommands>(yamlConfig.GuildId);
            appCommands.RegisterGuildCommands<PlayerCommands>(yamlConfig.GuildId);
            
            // Register the event handlers
            discord.RegisterEventHandler<EmbedButtons>();

            // Connect to Discord and Lavalink node
            await discord.ConnectAsync(new DiscordActivity(yamlConfig.ActivityName, yamlConfig.ActivityType), yamlConfig.Status);
            try
            {
                await lavalink.ConnectAsync(lavalinkConfig);
                Log.Logger.Information($"Lavalink Node [{yamlConfig.LavalinkHost}:{yamlConfig.LavalinkPort}] - Connected.");
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Lavalink Node [{yamlConfig.LavalinkHost}:{yamlConfig.LavalinkPort}] - Connection Failed.\n{ex}");
            }
            await Task.Delay(-1); // Prevent the bot from exiting
        }
    }
}
