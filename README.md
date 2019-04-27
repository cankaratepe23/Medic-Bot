# Medic-Bot || A (very painful) DSharpPlus Bot
If you want to run this bot on your own machine (for some reason?), you can clone this repo, or [the pre-built binaries](https://github.com/Mmedic23/MedicBotCoreReleases).

The source is targeted for .NET Framework v4.7.2, but I don't think you need the csproj files, you should be able to create your own project for whatever version you have installed and the source code should work fine.

The pre-built binaries, however, are built for .NET Core v2.2. The code doesn't change at all actually, but if you want to run on Linux or Mac, you need .NET Core. (Mono could work but DSharpPlus doesn't support it officially.)

This program currently uses DSharpPlus Nigthly 560, which means you might not able to `nuget restore` it easily. Nightly builds are available on MyGet, and you can get the links from DSharpPlus' GitHub repo.

Whichever version you're running, you'll need an environment variable called `Bot_Token` which is the token for the Discord bot application you intend to use.

You will also need to have ffmpeg and youtube-dl installed on your system and available in the PATH.

You'll need to install libopus and libsodium for your system. You can refer to the DSharpPlus documentation for help with this, but basically, if you're on Linux/Mac, you simply install them from your package manager; if you're on Windows, you'll need to get the binaries and put them next to your compiled executable.

The prefix of the bot is `#` and it has built-in help for commands. Altough most things are written mainly in Turkish.
