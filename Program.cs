using DisCatSharp.ApplicationCommands;
using DisCatSharp.Entities;
using DisCatSharp.Interactivity;
using DisCatSharp.Interactivity.Extensions;
using RadiSharp.Commands;
using Microsoft.Extensions.Logging;
using RadiSharp.Libraries.Managers;
using RadiSharp.Libraries.Utilities;
using RadiSharp.Typedefs;
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
        
        /// <summary>
        /// The main bot loop.
        /// </summary>
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
                yamlConfig = yamlDeserializer.Deserialize<YamlConfig>(await File.ReadAllTextAsync("config.canary.yml"));
#else
                yamlConfig = yamlDeserializer.Deserialize<YamlConfig>(await File.ReadAllTextAsync("config.yml"));
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                Log.Logger.Fatal($"Could not load config.canary.yml - Abort.\n{ex}");
#else
                Log.Logger.Fatal($"Could not load config.yml - Abort.\n{ex}");
#endif
                await Task.Delay(5000);
                Environment.ExitCode = 2;
                return;
            }
#if DEBUG
            Log.Logger.Information("config.canary.yml successfully loaded.");
#else
            Log.Logger.Information("config.yml successfully loaded.");
#endif

            // Initialize the bot
            var discord = new DiscordClient(new DiscordConfiguration()
            {
                Token = yamlConfig.BotSettings.Token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContent,
                LoggerFactory = loggerFactory
            });

            discord.UseInteractivity(new InteractivityConfiguration
            {
                Timeout = TimeSpan.FromSeconds(30)
            });

            // Initialize the Lavalink config
            var nodeManager = new NodeManager(discord, yamlConfig.LavalinkSettings);

            // Register the slash commands
            // NOTE: Global commands only update every hour, so it's recommended to use guild commands during development
            var appCommands = discord.UseApplicationCommands();
            
            if (yamlConfig.BotSettings.GlobalCommands)
            {
                appCommands.RegisterGlobalCommands<RadiSlashCommands>();
                appCommands.RegisterGlobalCommands<PlayerCommands>();
            }
            else
            {
                appCommands.RegisterGuildCommands<RadiSlashCommands>(yamlConfig.BotSettings.GuildId);
                appCommands.RegisterGuildCommands<PlayerCommands>(yamlConfig.BotSettings.GuildId);
            }
            
            // Register the event handlers
            discord.RegisterEventHandler<EmbedButtons>();

            // Connect to Discord and Lavalink node
            // For Docker deployments, wait 5 seconds before connecting to Lavalink to ensure the container is ready
            await discord.ConnectAsync(new DiscordActivity(yamlConfig.BotSettings.Activity.Name, yamlConfig.BotSettings.Activity.Type), yamlConfig.BotSettings.Activity.Status);
            await Task.Delay(5000);
            await nodeManager.Connect();
            await Task.Delay(-1); // Prevent the bot from exiting
        }
    }
}
