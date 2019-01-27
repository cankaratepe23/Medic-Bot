﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MedicBot
{
    class Program
    {
        // This list was written to handle RythmBot commands but turns out, RyhtmBot ignores bot messages.
        // Maybe test this in the future with Music Bot? Although RyhtmBot is better than MusicBot..
        // static readonly List<string> bannedWords = new List<string>(File.ReadLines(@"D:\Dan\Discord\MedicBot 2.0\MedicBot\MedicBot\banned_words.txt", System.Text.Encoding.UTF8));
        static List<ulong> alreadyPlayedForUsers = new List<ulong>();
        static DiscordClient discord;
        static CommandsNextExtension commands;
        static InteractivityExtension interactivity;
        static VoiceNextExtension voice;
        static void Main(string[] args)
        {
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        static async Task MainAsync(string[] args)
        {
            discord = new DiscordClient(new DiscordConfiguration
            {
                Token = Environment.GetEnvironmentVariable("Bot_Token", EnvironmentVariableTarget.User),
                TokenType = TokenType.Bot,
                UseInternalLogHandler = true,
                LogLevel = LogLevel.Debug
            });
            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                CaseSensitive = false,
                EnableDms = false,
                StringPrefixes = new string[] { "#" }
            });
            commands.RegisterCommands<MedicCommands>();
            interactivity = discord.UseInteractivity(new InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(1)
            });
            // EnableIncoming = true increases CPU usage and is not being used until Speech Recognition can be handled easily.
            /*
            voice = discord.UseVoiceNext(new VoiceNextConfiguration
            {
                EnableIncoming = true
            });
            */
            voice = discord.UseVoiceNext();


            discord.VoiceStateUpdated += async e =>
            {

                if (voice.GetConnection(e.Guild) != null && e.Channel == voice.GetConnection(e.Guild).Channel) //Remove(d) second check so bot can play audio for itself??   (&& e.User != discord.CurrentUser)
                {
                    if (voice.GetConnection(e.Guild) != null && !alreadyPlayedForUsers.Contains(e.User.Id))
                    {
                        Random rnd = new Random();
                        DiscordUser medicUser = await discord.GetUserAsync(134336937224830977);
                        List<string> userSpecificFiles = new List<string>(Directory.GetFiles(@"..\..\res\0\", "*.mp3"));
                        if (Directory.Exists(@"..\..\res\" + e.User.Id.ToString() + @"\"))
                        {
                            userSpecificFiles.AddRange(Directory.GetFiles(@"..\..\res\" + e.User.Id.ToString() + @"\", "*.mp3"));
                        }
                        string audioFile = Path.GetFileNameWithoutExtension(userSpecificFiles[rnd.Next(0, userSpecificFiles.Count)]);
                        await Task.Delay(1000);
                        //await commands.SudoAsync(medicUser, e.Guild.Channels.FirstOrDefault(), "#play " + audioFile);
                        await commands.ExecuteCommandAsync(commands.CreateFakeContext(medicUser, e.Guild.Channels.FirstOrDefault(), "#play " + audioFile, "#", commands.RegisteredCommands.Where(c => c.Key == "play").FirstOrDefault().Value, audioFile));
                        alreadyPlayedForUsers.Add(e.User.Id);
                    }
                }
                else if (e.Channel == null)
                {
                    alreadyPlayedForUsers.Remove(e.User.Id);
                }
            };

            discord.MessageCreated += async e =>
            {
                if (e.Author.Id == 477504775907311619 && e.Message.Content == "wrong")
                {
                    DiscordUser medicUser = await discord.GetUserAsync(134336937224830977);
                    //await commands.SudoAsync(medicUser, e.Channel, "#play wrong");
                    await commands.ExecuteCommandAsync(commands.CreateFakeContext(medicUser, e.Guild.Channels.FirstOrDefault(), "#play wrong", "#", commands.RegisteredCommands.Where(c => c.Key == "play").FirstOrDefault().Value, "wrong"));
                }
                else if (e.Message.Content.ToUpper().StartsWith("HOFFMAN"))
                {
                    await e.Channel.SendMessageAsync("Yeah?");
                    var userReply = await interactivity.WaitForMessageAsync(m => m.Author == e.Author && m.Content.ToLower().Contains(" call this "));
                    await e.Channel.SendMessageAsync("Uh, uhh...");
                    // TODO: Think of functionality for this HOFFMAN
                }
            };
            await discord.ConnectAsync();
            await Task.Delay(-1);
        }
    }
}