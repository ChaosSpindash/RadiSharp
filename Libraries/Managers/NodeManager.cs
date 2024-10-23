using DisCatSharp.Lavalink;
using DisCatSharp.Lavalink.EventArgs;
using DisCatSharp.Net;
using RadiSharp.Typedefs;
using Serilog;

namespace RadiSharp.Libraries.Managers;

/// <summary>
/// Manages the connection to Lavalink nodes.
/// </summary>
public class NodeManager
{
    /// <summary>
    /// The current instance of NodeManager. Only one instance can exist at a time.
    /// </summary>
    public static NodeManager? Instance { get; private set; }

    /// <summary>
    /// The list of Lavalink nodes to connect to.
    /// </summary>
    private List<LavalinkNode> Nodes { get; set; }

    /// <summary>
    /// The list of Lavalink configurations for each node.
    /// </summary>
    private List<LavalinkConfiguration> Configurations { get; set; } = [];

    /// <summary>
    /// The number of nodes in the list.
    /// </summary>
    private int NodeCount => Nodes.Count;

    /// <summary>
    /// The index of the current node being connected to.
    /// </summary>
    private int CurrentNode { get; set; }

    /// <summary>
    /// The timeout in milliseconds before retrying to connect to a node.
    /// </summary>
    private int Timeout { get; }

    /// <summary>
    /// The amount of full node rotations without a successful connection.
    /// </summary>
    private int FullRetryCount { get; set; }

    /// <summary>
    /// The maximum amount of full node rotations without a successful connection.
    /// If this limit is reached, no further connection attempts will be made.
    /// </summary>
    private int MaxFullRetries { get; } = 3;

    /// <summary>
    /// The Discord client to get the Lavalink client from.
    /// </summary>
    private DiscordClient Client { get; set; }
    
    /// <summary>
    /// The Lavalink extension of the Discord client.
    /// </summary>
    private LavalinkExtension Lavalink { get; set; }

    /// <summary>
    /// Instantiates a new NodeManager. Only one instance can exist at a time.
    /// </summary>
    /// <param name="discord">The Discord client to get the Lavalink client from.</param>
    /// <param name="lavalink">The Lavalink settings containing the list of nodes and other parameters.</param>
    public NodeManager(DiscordClient discord, LavalinkSettings lavalink)
    {
        // Import the Discord client and Lavalink settings
        Client = discord;
        Timeout = lavalink.Timeout;
        Nodes = lavalink.Nodes;

        // Initialize the Lavalink client
        Lavalink = Client.UseLavalink();

        // Create a Lavalink configuration for each node
        foreach (LavalinkNode node in Nodes)
        {
            var endpoint = new ConnectionEndpoint(node.Host, node.Port, node.Secure);
            var lavalinkConfig = new LavalinkConfiguration
            {
                Password = node.Pass,
                RestEndpoint = endpoint,
                SocketEndpoint = endpoint,
#if DEBUG
                EnableTrace = true
#endif
            };
            Configurations.Add(lavalinkConfig);
        }

        // Register the event handler for connection loss
        Lavalink.SessionDisconnected += ConnectionLostEventHandler;

        // Set this as the current instance
        Instance = this;
    }

    /// <summary>
    /// Attempts to connect to one of the saved Lavalink nodes.
    /// </summary>
    /// <remarks>
    /// NodeManager keeps track of the number of full node rotations. If a full rotation is made without a successful connection,
    /// a timeout is triggered before the next connection attempt.
    /// <para>
    /// If the maximum number of full rotations is reached, no further
    /// attempts will be made. It is then assumed that all nodes are offline or unreachable.
    /// </para>
    /// </remarks>
    public async Task Connect()
    {
        // If no nodes are found, log an error and return
        if (NodeCount == 0)
        {
            Log.Logger.Error("ERR_CFG_NO_NODES_FOUND: No nodes found in the configuration file.");
            return;
        }

        // If the current node index exceeds the number of nodes, reset it
        if (CurrentNode >= NodeCount)
        {
            CurrentNode = 0;
        }

        Lavalink = Client.GetLavalink();
        
        // Attempt to connect to the current node
        try
        {
            await Lavalink.ConnectAsync(Configurations[CurrentNode]);
            Log.Logger.Information(
                $"Connected to Lavalink node [{Nodes[CurrentNode].Host}:{Nodes[CurrentNode].Port}].");
            // Reset the full rotation count if a successful connection is made
            FullRetryCount = 0;
            CurrentNode = CurrentNode;
        }
        // If the connection attempt fails, try the next node in the list
        catch (Exception ex)
        {
            Log.Logger.Warning(
                $"ERR_LAVALINK_CONN_FAILED: {(ex.InnerException is not null ? ex.InnerException.Message : ex.Message)}.");
            if (CurrentNode == NodeCount - 1)
            {
                // If the last node in the list is reached, increment the full rotation count
                FullRetryCount++;
                if (FullRetryCount >= MaxFullRetries)
                {
                    // If the maximum number of full rotations is reached, abort connection attempts
                    // This is to prevent infinite recursion of the method if all nodes are offline
                    Log.Logger.Fatal(
                        $"ERR_LAVALINK_ROTATION_ABORT: Failed to connect to all nodes after {MaxFullRetries} full rotations. Aborting connection attempts.");
                    return;
                }

                // If a full rotation is made without a successful connection, wait before trying again
                Log.Logger.Error(
                    $"ERR_LAVALINK_ROTATION_TIMEOUT: Failed to connect to all nodes. Awaiting {Timeout / 1000} seconds before retrying...");
                await Task.Delay(Timeout);
            }
            // Attempt to connect to the next node in the list
            CurrentNode++;
            Log.Logger.Warning("Attempting to connect to the next node...");
            await Connect();
        }
    }

    /// <summary>
    /// Event handler for when the connection to a Lavalink node is lost. If the connection was not closed cleanly,
    /// it will attempt to reconnect to the node.
    /// </summary>
    /// <param name="s">The Lavalink extension of the Discord client.</param>
    /// <param name="e">The arguments for the LavalinkSessionDisconnected event.</param>
    private async Task ConnectionLostEventHandler(LavalinkExtension s, LavalinkSessionDisconnectedEventArgs e)
    {
        // If the connection was closed cleanly, log the event
        if (e.IsCleanClose)
        {
            Log.Logger.Information(
                $"Connection to Lavalink node [{Nodes[CurrentNode].Host}:{Nodes[CurrentNode].Port}] closed.");
        }
        // Otherwise, attempt to reconnect to the node
        else
        {
            Log.Logger.Warning(
                $"ERR_LAVALINK_CONN_LOST: Connection to Lavalink node [{Nodes[CurrentNode].Host}:{Nodes[CurrentNode].Port}] lost. Attempting to reconnect...");
            await Connect();
        }
    }
}