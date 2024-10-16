# RadiSharp
A .NET Discord music bot implementation based on DisCatSharp and Lavalink.
**[README WORK IN PROGRESS]**

## Features
- Lavalink v4 support
- Custom queue manager with playlist support
- "Search & Queue" for YouTube
- Button interactions for player and queue

## Setup
1. Create a new application in the Discord Developer Portal.
  - Disable **Public Bot** if you do not want others to invite your bot.
  - For Search & Queue to work, make sure you have enabled **Message Content** in your **Privileged Intents**.
> [!NOTE]
> Enabling Privileged Intents while active in 100+ servers **requires your bot to be whitelisted by Discord Staff.**
2. Invite your bot to your server. (TODO: Permissions)
3. Copy the example config file and rename it to `config.yaml`.
  - If you are running the bot in a debug session, it will look for `config.canary.yaml` instead.
4. Fill out your config with the following:
  - **Bot Token**: reset your bot token in the Bot tab of the Discord Developer Portal. You will be shown a fresh token to copy into your config.
> [!CAUTION]
> **Do NOT share the bot token with anyone else. Do NOT hardcode it or commit it to Git.**
> 
> If this token is public, bad actors can gain unauthorized access to your bot and potentially cause damage to the servers it's been invited to.
> 
> If you think this is the case, you should reset your bot token **IMMEDIATELY.**
  - (Optional) **Activity Settings**: This is the activity status the bot will use at startup, e.g. "Listening to Dustbowle Radio".
  - **Guild ID**: Enter the ID of the guild you want to use your bot in. This is used to register the slash commands. If the bot is set to register global commands, this field is ignored.
> [!NOTE]
> Keep in mind that global commands can only be updated **every hour**. For testing and debug purposes, using guild commands is recommended.
  - **Lavalink Server**: Enter the address, port and password of the Lavalink node to connect to. Enable SSL if required.

## Deploy
### Docker
[Coming Soon]

### Non-Docker
RadiSharp comes as a self-contained .NET binary for both Linux and Windows. Installation of the .NET 8 Runtime on the host is not required.
The only other file required alongside the binary is `config.yaml`.

You may also want to set up a service/task to ensure the bot auto-starts with the host.

#### Linux (systemd)
Create a file named `radisharp.service` in `/usr/lib/systemd/system` and populate it as shown in the example below.

#### Windows (Task Scheduler)
- Create a new task in the Task Scheduler and name it `RadiSharp` (or choose your own name).
- Enable `Run whether user is logged on or not`. Make sure the user has sufficient privileges to run the bot.
- Add a trigger to run the task at startup.
- Add an action to run the RadiSharp executable.
- If you intend to run the bot on a laptop or similar, disable `Start the task only if the computer is on AC power` in the `Conditions` tab.
- In `Settings`, allow the task to restart automatically if it fails and disable `Stop the task if it runs longer than:` to prevent it from stopping after 3 days.

