# RadiSharp
A .NET Discord music bot implementation based on DisCatSharp and Lavalink.

## Features
- Lavalink v4 support
- Custom Lavalink node manager with automatic reconnect and failover
- Custom queue manager with playlist support
- "Search & Queue" for YouTube
- Button interactions for player and queue

## Setup

> [!CAUTION]
> **Do NOT share the bot token with anyone else. Do NOT hardcode it or commit it to Git.**
>
> If this token is public, bad actors can gain unauthorized access to your bot and potentially cause damage to the servers it's been invited to.
>
> If you think this is the case, you should reset your bot token **IMMEDIATELY.**

1. Create a new application in the [Discord Developer Portal](https://discord.com/developers/applications).
    - For Search & Queue to work, make sure you have enabled **Message Content** in your **Privileged Intents**. Enabling Privileged Intents while active in 100+ servers **requires your bot to be whitelisted by Discord Staff.**
2. Invite your bot to your server by generating an OAuth2 URL with the `bot` and `applications.commands` scopes. Non-administrative permissions are sufficient, but this is subject to change.
3. Copy the example config file and rename it to `config.yml`.
   - If you are running the bot in a debug session, it will look for `config.canary.yml` instead.
4. Fill out your config with the following:
   - **Bot Token**: reset your bot token in the Bot tab of the Discord Developer Portal. You will be shown a fresh token to copy into your config.
   - (Optional) **Activity Settings**: This is the activity status the bot will use at startup, e.g. "Listening to Dustbowle Radio".
   - **Guild ID**: Enter the ID of the guild you want to use your bot in. This is used to register local slash commands.
   - **Global Commands**: Set this to `true` if you want to register global commands instead.
       - Global commands are available in all guilds the bot is in, but may take up to an hour to propagate. 
   - **Lavalink Nodes**: Enter the address, port and password of the Lavalink nodes to connect to. Enable SSL if required.

## Deploy
### Docker
RadiSharp comes with a Docker Compose file for easy deployment. This also gives you the option to run a local Lavalink node alongside the bot.

1. Clone the repository to your Docker host.
2. Set up the `config.yml` file as described above.
3. (Optional) Create a `application.yml` file inside the `Lavalink` directory to configure the Lavalink node.
    - An example is provided in the `Lavalink` directory.
4. Run `docker compose up -d` to start the bot and Lavalink node. Append `radisharp` to the command to start only the bot.

### Non-Docker
RadiSharp also comes as a self-contained .NET binary for both Linux and Windows. Installation of the .NET 9 Runtime on the host is not required.
The only other file required alongside the binary is `config.yml`.

You may also want to set up a service/task to ensure the bot auto-starts with the host.
- **Linux**: Use [systemd](https://linuxhandbook.com/create-systemd-services/) or [pm2](https://pm2.keymetrics.io/docs/usage/quick-start/).
- **Windows**: Use [Task Scheduler](https://www.windowscentral.com/how-create-automated-task-using-task-scheduler-windows-10).

Local Lavalink nodes need to be set up separately. [Follow the official Lavalink guide](https://lavalink.dev/getting-started/index.html) for more information.

## Notes
- RadiSharp is a hobby project and is not intended for production use.
- The bot currently uses a modified version of DisCatSharp, primarily the Lavalink assembly. The original assembly has issues that prevent NodeManager from working correctly. Changes can be found in my fork: [ChaosSpindash/DisCatSharp](https://github.com/ChaosSpindash/DisCatSharp)

