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

            BotConfig? botConfig = null;

            #region JSON Parser
            try
            {
#if DEBUG
                botConfig = JsonSerializer.Deserialize<BotConfig>(await File.ReadAllTextAsync("config-canary.json"));
                Log.Logger.Information("Successfully loaded config-canary.json.");
#else
                botConfig = JsonSerializer.Deserialize<BotConfig>(await File.ReadAllTextAsync("config.json"));
                Log.Logger.Information("Successfully loaded config.json.");
#endif
            }
            catch (Exception ex)
            {
                Log.Logger.Fatal($"Failed to load JSON config.\n{ex}");
            }
            #endregion
            
            #region YAML Parser (deprecated)

            if (botConfig is null)
            {
                try
                {
#if DEBUG
                    botConfig = yamlDeserializer.Deserialize<BotConfig>(
                        await File.ReadAllTextAsync("config-canary.yml"));
                    Log.Logger.Information("Successfully loaded config-canary.yml.");
#else
                botConfig = yamlDeserializer.Deserialize<BotConfig>(await File.ReadAllTextAsync("config.yml"));
                Log.Logger.Information("Successfully loaded config.yml.");
#endif
                    Log.Logger.Warning("Support for YAML is deprecated and will be removed in a future update.");
                }
                catch (Exception ex)
                {
                    Log.Logger.Fatal($"Failed to load YAML config.\n{ex}");
                }
            }

            #endregion
            
            if (botConfig is null)
            {
                Log.Logger.Fatal("Failed to load the bot configuration. Exiting...");
                await Task.Delay(5000);
                Environment.ExitCode = 2;
                return;
            }

            // Initialize the bot
            var discord = new DiscordClient(new DiscordConfiguration()
            {
                Token = botConfig.BotSettings.Token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContent,
                LoggerFactory = loggerFactory
            });

            discord.UseInteractivity(new InteractivityConfiguration
            {
                Timeout = TimeSpan.FromSeconds(30)
            });

            // Initialize the Lavalink config
            var nodeManager = new NodeManager(discord, botConfig.LavalinkSettings);

            // Register the slash commands
            // NOTE: Global commands only update every hour, so it's recommended to use guild commands during development
            var appCommands = discord.UseApplicationCommands();
            
            if (botConfig.BotSettings.GlobalCommands)
            {
                appCommands.RegisterGlobalCommands<RadiSlashCommands>();
                appCommands.RegisterGlobalCommands<PlayerCommands>();
            }
            else
            {
                appCommands.RegisterGuildCommands<RadiSlashCommands>(botConfig.BotSettings.GuildId);
                appCommands.RegisterGuildCommands<PlayerCommands>(botConfig.BotSettings.GuildId);
            }
            
            // Register the event handlers
            discord.RegisterEventHandler<EmbedButtons>();

            // Connect to Discord and Lavalink node
            // For Docker deployments, wait 5 seconds before connecting to Lavalink to ensure the container is ready
            await discord.ConnectAsync(new DiscordActivity(botConfig.BotSettings.Activity.Name, botConfig.BotSettings.Activity.Type), botConfig.BotSettings.Activity.Status);
            await Task.Delay(5000);
            await nodeManager.Connect();
            await Task.Delay(-1); // Prevent the bot from exiting
        }
    }
}
