using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using YoutubeSearch;
using DSharpPlus.EventArgs;

namespace MedicBot
{
    [RequirePrefixes("#")]
    public class MedicCommands : DSharpPlus.CommandsNext.BaseCommandModule
    {
        private ConcurrentDictionary<uint, Process> ffmpegs;
        bool checkAudioExists = true;
        bool nowPlaying;
        bool recordingDisabled = true;
        private List<string> queuedSongs = new List<string>();

        #region Commands related to connectivity.
        [Command("disconnect")]
        [Aliases("siktir", "siktirgit", "sg")]
        [Description("Botu, isteğe göre duygularını inciterek, komple kapatır.")]
        public async Task Disconnect(CommandContext ctx)
        {
            if (ctx.User.Id != 134336937224830977)
            {
                DiscordUser medicUser = await ctx.Guild.GetMemberAsync(134336937224830977);
                await ctx.RespondWithFileAsync(@"..\..\res\hahaha_no.gif", "Bu komutu sadece HACKERMAN yani KRAL yani " + medicUser.Mention + " kullanabilir.");
                return;
            }
            //Log("DISCONNECT: Got disconnect signal.");
            await ctx.Client.DisconnectAsync();
            //Log("DISCONNECT: Client disconnected BY " + ctx.User.Username);
            Environment.Exit(0);
        }

        [Command("join")]
        [Aliases("katıl", "gel")]
        [Description("Botu ses kanalına çağırır.")]
        public async Task Join(CommandContext ctx, ulong channelID = 0)
        {
            var voiceNext = ctx.Client.GetVoiceNext();
            var voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
            if (voiceNextConnection != null)
            {
                await ctx.RespondAsync("Buradayız işte lan ne join atıyon");
                throw new InvalidOperationException("Already connected, no need to reconnect.");
            }
            if (channelID == 0 && ctx.Member.VoiceState.Channel == null)
            {
                await ctx.RespondAsync("Önden bayanlar");
                throw new InvalidOperationException("You need to be in a voice channel.");
            }
            else if (channelID == 0)
            {
                voiceNextConnection = await voiceNext.ConnectAsync(ctx.Member.VoiceState.Channel);
                //Log(String.Format("JOIN: Bot joined to voice channel {0}({1})", voiceNextConnection.Channel.Id, voiceNextConnection.Channel.Name));
            }
            else
            {
                voiceNextConnection = await voiceNext.ConnectAsync(ctx.Guild.GetChannel(channelID));
                //Log(String.Format("JOIN: Bot joined to voice channel {0}({1})", voiceNextConnection.Channel.Id, voiceNextConnection.Channel.Name));
            }
            this.ffmpegs = new ConcurrentDictionary<uint, Process>();
            voiceNextConnection.VoiceReceived += OnVoiceReceived;
        }

        [Command("join")]
        [Description("Botu ses kanalına çağırır.")]
        public async Task Join(CommandContext ctx, string channelName)
        {
            var voiceNext = ctx.Client.GetVoiceNext();
            var voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
            if (voiceNextConnection != null)
            {
                await ctx.RespondAsync("Buradayız işte lan ne join atıyon");
                throw new InvalidOperationException("Already connected, no need to reconnect.");
            }
            IEnumerable<DiscordChannel> voiceChannels = ctx.Guild.Channels.Where(ch => ch.Type == DSharpPlus.ChannelType.Voice && ch.Name == channelName);
            if (voiceChannels.Count() == 1)
            {
                voiceNextConnection = await voiceNext.ConnectAsync(voiceChannels.FirstOrDefault());
            }
            else
            {
                await ctx.RespondAsync("Ses kanalı bulunamadı ya da birden fazla bulundu. Biraz daha kesin konuşur musun");
                throw new InvalidOperationException("Multiple or no voice channels found.");
            }
            this.ffmpegs = new ConcurrentDictionary<uint, Process>();
            voiceNextConnection.VoiceReceived += OnVoiceReceived;
        }

        [Command("leave")]
        [Aliases("ayrıl", "git", "çık")]
        [Description("Botu ses kanalından kovar.")]
        public async Task Leave(CommandContext ctx)
        {
            var voiceNext = ctx.Client.GetVoiceNext();
            var voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
            if (voiceNextConnection == null)
            {
                await ctx.RespondAsync("Daha gelmedik ki kovuyorsun");
                throw new InvalidOperationException("Not connected, can't leave.");
            }
            voiceNextConnection.VoiceReceived -= OnVoiceReceived;
            foreach (var kvp in this.ffmpegs)
            {
                await kvp.Value.StandardInput.BaseStream.FlushAsync();
                kvp.Value.StandardInput.Dispose();
                kvp.Value.WaitForExit();
            }
            this.ffmpegs = null;
            //Log(String.Format("LEAVE: Bot is leaving voice channel {0}({1})", voiceNextConnection.Channel.Id, voiceNextConnection.Channel.Name));
            voiceNextConnection.Disconnect();
            //Log("LEAVE: Bot left the voice channel.");
        }
        #endregion

        #region Commands related to playback.
        [Command("stop")]
        [Aliases("dur", "durdur")]
        [Description("Bot ses çalıyorsa susturur.")]
        public async Task Stop(CommandContext ctx)
        {
            queuedSongs.Clear();
            await ctx.Client.UpdateStatusAsync();
            await Leave(ctx);
            await Join(ctx);
        }

        [Command("play")]
        [Aliases("oynatbakalım")]
        [Description("Bir ses oynatır.")]
        public async Task Play(CommandContext ctx, [Description("Çalınacak sesin adı. `#liste` komutuyla tüm seslerin listesini DM ile alabilirsiniz.")][RemainingText] string fileName)
        {
            string filePath;
            if (fileName != null)
            {
                filePath = @"..\..\res\" + fileName + ".mp3";
            }
            else
            {
                Random rnd = new Random();
                string[] allFiles = Directory.GetFiles(@"..\..\res\");
                filePath = allFiles[rnd.Next(allFiles.Length)];
            }
            var voiceNext = ctx.Client.GetVoiceNext();
            var voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
            if (voiceNextConnection == null)
            {
                await ctx.RespondAsync("Daha gelmedim bi dur");
                throw new InvalidOperationException("Not connected, can't play.");
            }
            if (!File.Exists(filePath))
            {
                await ctx.RespondAsync("Öyle bir şey yok. ._.");
                throw new InvalidOperationException("File not found.");
            }

            if (nowPlaying)
            {
                queuedSongs.Add(Path.GetFileNameWithoutExtension(filePath));
                return;
            }
            await voiceNextConnection.SendSpeakingAsync(true);
            nowPlaying = true;
            await ctx.Client.UpdateStatusAsync(new DiscordActivity(Path.GetFileNameWithoutExtension(filePath), ActivityType.Playing));

            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $@"-v quiet -stats -i ""{filePath}"" -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var ffmpeg = Process.Start(psi);
            var ffout = ffmpeg.StandardOutput.BaseStream;
            var buff = new byte[3840];
            var br = 0;
            while ((br = ffout.Read(buff, 0, buff.Length)) > 0)
            {
                if (br < buff.Length)
                {
                    for (var i = br; i < buff.Length; i++)
                    {
                        buff[i] = 0;
                    }
                }

                await voiceNextConnection.SendAsync(buff, 20);
            }
            await voiceNextConnection.SendSpeakingAsync(false);
            nowPlaying = false;
            if (queuedSongs.Count != 0)
            {
                string onQueue = queuedSongs.First();
                queuedSongs.RemoveAt(0);
                //await ctx.CommandsNext.SudoAsync(ctx.User, ctx.Channel, "#play " + onQueue);
                await ctx.CommandsNext.ExecuteCommandAsync(ctx.CommandsNext.CreateFakeContext(ctx.User, ctx.Channel, "#play " + onQueue, "#", ctx.CommandsNext.RegisteredCommands.Where(c => c.Key == "play").FirstOrDefault().Value, onQueue));
            }
            await ctx.Client.UpdateStatusAsync();
        }
        #endregion

        #region Commands related to metadata requests.
        [Command("liste")]
        [Description("Botun çalabileceği tüm seslerin listesi. Her zaman günceldir.")]
        public async Task List( //karakter limitini aşmamak için response'u böl
            CommandContext ctx,
            [Description("Seslerin içinde aranacak harf/kelime")][RemainingText]string searchString)
        {
            string[] allFiles = Directory.GetFiles(@"..\..\res\", "*.mp3");
            string response = "```\n";
            if (searchString != null)
            {
                response = response.Insert(0, "`" + searchString + "` için sonuçlar gösteriliyor.");
            }
            int fileAddedToResponseCount = 0;
            foreach (string file in allFiles)
            {
                DateTime modifiedDate = File.GetLastWriteTime(file); //use GetLastWriteTime to get the date the file was first download. this date is not affected by deletion, unlike GetCreationTime.
                TimeSpan fileAge = DateTime.Now - modifiedDate;
                string fileOnly = Path.GetFileNameWithoutExtension(file);
                if (searchString == null || fileOnly.Contains(searchString))
                {
                    if (fileAge > TimeSpan.FromDays(2))
                    {
                        response += "[ • ] " + fileOnly + "\n";
                    }
                    else
                    {
                        response += "[NEW] " + fileOnly + "\n";
                    }
                    fileAddedToResponseCount++;
                    if (fileAddedToResponseCount >= 100)
                    {
                        response += "```";
                        await ctx.Member.SendMessageAsync(response);
                        response = "```\n";
                        fileAddedToResponseCount = 0;
                    }
                }
            }
            if (response == "`" + searchString + "` için sonuçlar gösteriliyor.```\n")
            {
                response += "Ses dosyası bulunamadı.";
            }
            response += "```";
            await ctx.Member.SendMessageAsync(response);
        }


        [Command("link")]
        [Description("Bir ses kaydının (varsa) indirildiği linki getirir.")]
        public async Task Link(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            ProcessStartInfo ffprobeInfo = new ProcessStartInfo()
            {
                FileName = "ffprobe.exe",
                Arguments = "-i \"" + audioName + ".mp3\" -v error -of default=noprint_wrappers=1:nokey=1 -hide_banner -show_entries format_tags=comment",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WorkingDirectory = @"..\..\res\"
            };
            string URL = "Bu dosya için link bulunamadı.";
            Process ffprobe = Process.Start(ffprobeInfo);
            while (!ffprobe.StandardOutput.EndOfStream)
            {
                URL = ffprobe.StandardOutput.ReadLine();
            }
            ffprobe.WaitForExit();
            ffprobe.Dispose();
            await ctx.RespondAsync(URL);
        }

        [Command("owner")]
        [Description("Bir ses kaydını ekleyen kişiyi (varsa) getirir.")]
        [Aliases("sahibi")]
        public async Task Owner(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            if (!File.Exists(@"..\..\res\" + audioName + ".mp3"))
            {
                await ctx.RespondAsync("Öyle bir şey yok. ._.");
                throw new InvalidOperationException("File not found.");
            }

            ProcessStartInfo ffprobeInfo = new ProcessStartInfo()
            {
                FileName = "ffprobe.exe",
                Arguments = "-i \"" + audioName + ".mp3\" -v error -of default=noprint_wrappers=1:nokey=1 -hide_banner -show_entries format_tags=author",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WorkingDirectory = @"..\..\res\"
            };
            string UId = "0";
            Process ffprobe = Process.Start(ffprobeInfo);
            while (!ffprobe.StandardOutput.EndOfStream)
            {
                UId = ffprobe.StandardOutput.ReadLine();
            }
            ffprobe.WaitForExit();
            ffprobe.Dispose();
            if (UId == "0")
            {
                await ctx.RespondAsync("Bu dosyanın sahibi bulunamadı.");
                throw new InvalidOperationException("File doesn't have an owner.");
            }
            DiscordUser discordUser = await ctx.Client.GetUserAsync(Convert.ToUInt64(UId));
            await ctx.RespondAsync("`" + audioName + "` sesinin sahibi: " + discordUser.Username);
        }
        #endregion

        #region Commands related to adding, managing and removing audio files.
        [Command("ekle")]
        [Aliases("indir")]
        [Description("Verilen linkteki sesi, verilen süre parametrelerine göre ayarlayıp botun ses listesinde çalınmak üzere ekler.")]
        public async Task Add(
            CommandContext ctx,
            [Description("Ses kaynağının linki. YouTube, Dailymotion ve başka video paylaşım sitelerini, video gömülü sayfaları deneyebilirsiniz. Çalışma garanitisi vermiyorum.")]string URL,
            [Description("İlgili bölümün, linkteki videoda başladığı saniye. Örn. 2:07 => 127 ya da 134.5")]string startSec,
            [Description("İlgili bölümün, linkteki videoda saniye cinsinden uzunluğu. Örn. 5.8")]string durationSec,
            [Description("Sesin kayıtlardaki adı. Örn. #play [gireceğiniz ad] komutuyla çalmak için.")][RemainingText]string audioName)
        {
            Log("ADD: Add command triggered BY " + ctx.User.Username + "(" + ctx.User.Id + ") :: " + ctx.Message.Content);
            if (checkAudioExists)
            {
                if (Directory.GetFiles(@"..\..\res\", "*.mp3").Contains(@"..\..\res\" + audioName + ".mp3"))
                {
                    await ctx.RespondAsync("Bu isimdeki ses kaydı zaten bota eklenmiştir.");
                    throw new InvalidOperationException("Audio file with same name already exists.");
                }
            }
            checkAudioExists = true;

            ProcessStartInfo ydlStartInfo = new ProcessStartInfo()
            {
                FileName = "youtube-dl.exe",
                Arguments = "--max-filesize 10M -f bestaudio " + URL + " -o \"" + audioName + ".webm\"",
                WorkingDirectory = @"..\..\res\işlenecekler\"
            };
            Process youtubeDl = Process.Start(ydlStartInfo);
            youtubeDl.WaitForExit();
            youtubeDl.Dispose();

            ProcessStartInfo ffmpegStartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg.exe",
                Arguments = "-ss " + startSec + " -t " + durationSec + " -i \"" + audioName + ".webm\" -b:a 128K -metadata comment=\"" + URL + "\" -metadata author=\"" + ctx.User.Id + "\" \"" + audioName + ".mp3\"",
                WorkingDirectory = @"..\..\res\işlenecekler\"
            };
            Process ffmpeg = Process.Start(ffmpegStartInfo);
            ffmpeg.WaitForExit();
            ffmpeg.Dispose();
            File.Delete(@"..\..\res\işlenecekler\" + audioName + ".webm");
            if (File.Exists(@"..\..\res\" + audioName + ".mp3"))
            {
                File.Delete(@"..\..\res\" + audioName + ".mp3");
            }

            File.Move(@"..\..\res\işlenecekler\" + audioName + ".mp3", @"..\..\res\" + audioName + ".mp3");
            Log("ADD: Added " + audioName + " FROM " + URL);
        }

        [Command("intro")]
        [Aliases("giriş")]
        [Description("Ses kayıtları arasında halihazırda bulunan bir sesi giriş sesiniz olarak ayarlar.")]
        public async Task Intro(
            CommandContext ctx,
            [RemainingText]string audioName)
        {
            if (File.Exists(@"..\..\res\" + ctx.User.Id.ToString() + @"\" + audioName + ".mp3"))
            {
                await ctx.RespondAsync("\"" + audioName + "\" ses efekti zaten sizin giriş efektiniz olarak kayıtlı.");
                throw new InvalidOperationException("An audio file with the same name is already added to the user's audio list.");
            }
            if (!File.Exists(@"..\..\res\" + audioName + ".mp3"))
            {
                await ctx.RespondAsync("Öyle bir şey yok. ._.");
                throw new InvalidOperationException("File not found.");
            }
            File.Copy(@"..\..\res\" + audioName + ".mp3", @"..\..\res\" + ctx.User.Id.ToString() + @"\" + audioName + ".mp3");
            Log(string.Format("INTRO: {0} set as intro FOR {1}({2})", audioName, ctx.User.Username, ctx.User.Id));
        }


        [Command("edit")]
        [Aliases("değiştir")]
        [Description("Ses kayıtları arasında halihazırda bulunan bir sesi yeniden indirip keserek değiştirir.")]
        public async Task Edit(
            CommandContext ctx,
            [Description("Ses kaynağının linki. YouTube, Dailymotion ve başka video paylaşım sitelerini, video gömülü sayfaları deneyebilirsiniz. Çalışma garanitisi vermiyorum.")]string URL,
            [Description("İlgili bölümün, linkteki videoda başladığı saniye. Örn. 2:07 => 127 ya da 134.5")]string startSec,
            [Description("İlgili bölümün, linkteki videoda saniye cinsinden uzunluğu. Örn. 5.8")]string durationSec,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            Log("EDIT: Edit command triggered BY " + ctx.User.Username + "(" + ctx.User.Id + ") :: " + ctx.Message.Content);
            if (!File.Exists(@"..\..\res\" + audioName + ".mp3"))
            {
                await ctx.RespondAsync("Bu isme sahip ses kaydı bulunamadı. Lütfen `#ekle` komutunu kullanın.");
                throw new InvalidOperationException("The file to edit doesn't exist.");
            }
            if (!IsOwner(ctx.User.Id, audioName))
            {
                await ctx.RespondAsync("Bu sesi, sahibi siz olmadığınız için değiştiremezsiniz.");
                throw new InvalidOperationException("The user trying to edit the file is not the owner of the file.");
            }
            checkAudioExists = false;
            Log(string.Format("EDIT: Command successfully accessed BY {0}({1}) FOR {2}", ctx.User.Username, ctx.User.Id, audioName));
            //await ctx.CommandsNext.SudoAsync(ctx.User, ctx.Channel, string.Format("#ekle {0} {1} {2} {3}", URL, startSec, durationSec, audioName));
            await ctx.CommandsNext.ExecuteCommandAsync(ctx.CommandsNext.CreateFakeContext(ctx.User, ctx.Channel, string.Format("#ekle {0} {1} {2} {3}", URL, startSec, durationSec, audioName), "#", ctx.CommandsNext.RegisteredCommands.Where(c => c.Key == "ekle").FirstOrDefault().Value, string.Format("{0} {1} {2} {3}", URL, startSec, durationSec, audioName)));
        }

        [Command("delete")]
        [Description("Bir ses kaydını siler.")]
        [Aliases("sil")]
        public async Task Delete(
            CommandContext ctx,
            [Description("Sesin kayıtlardaki adı.")][RemainingText]string audioName)
        {
            Log("DELETE: Delete command triggered BY " + ctx.User.Username + "(" + ctx.User.Id + ") :: " + ctx.Message.Content);
            if (!File.Exists(@"..\..\res\" + audioName + ".mp3"))
            {
                await ctx.RespondAsync("Öyle bir şey yok. ._.");
                throw new InvalidOperationException("File to delete not found.");
            }
            if (!IsOwner(ctx.User.Id, audioName))
            {
                await ctx.RespondAsync("Bu sesi, sahibi siz olmadığınız için silemezsiniz.");
                throw new InvalidOperationException("The user trying to delete the file is not the owner of the file.");
            }
            File.Copy(@"..\..\res\" + audioName + ".mp3", @"..\..\res\trash\" + audioName + ".mp3"); //Copy and then Delete instead of Move so the Date Created property updates to reflect the date and time the sound file was deleted.
            File.Delete(@"..\..\res\" + audioName + ".mp3"); //Using Move leads to the Date Created and Date Modified properties not change at all.
            Log(string.Format("DELETE: FILE {0} deleted BY {1}({2})", audioName, ctx.User.Username, ctx.User.Id));
            await ctx.RespondAsync("🗑️");
        }
        #endregion

        [Command("youtube")]
        [Description("Verilen sözcüğü/sözcükleri youtube'da arar ve ilk (X) sonucu yazar.")]
        public async Task Youtube(
            CommandContext ctx,
            [RemainingText][Description("")] string mainString)
        {
            string searchString;
            VideoSearch items = new VideoSearch();

            if (int.TryParse(mainString.Split(' ').Last(), out int itemCount)) // if last part of the mainString sent with the command is a number
            {
                searchString = mainString.Remove(mainString.LastIndexOf(' '));
                if (itemCount == 1)
                {
                    VideoInformation searchResult = items.SearchQuery(searchString, 1).FirstOrDefault();
                    await ctx.RespondAsync(searchResult.Url);
                    return;
                }

                InteractivityExtension interactivity = ctx.Client.GetInteractivity();
                int pageCount = (itemCount + 18) / 19;
                List<VideoInformation> searchResults = items.SearchQuery(searchString, pageCount); //(ADDED) Add logic so the page count changes depending on how many items the user requested (itemCount) ??? It seems to get 19 results per page
                searchResults.RemoveRange(itemCount, searchResults.Count - itemCount);
                string response = "```\n-----------------------------------------------------------------\n";
                int i = 1;
                foreach (VideoInformation video in searchResults)
                {
                    response += "[" + i + "] " + video.Title + " ||by|| " + video.Author + "\n" + video.Url + " (" + video.Duration + ")" + "\n-----------------------------------------------------------------\n";
                    i++;
                }
                response += "```";
                if (response.Length > 2000)
                {
                    await ctx.RespondAsync("Karakter limitine ulaşıldı. Lütfen daha az video arayın.");
                    throw new InvalidOperationException("Message too long to send.");
                }
                await ctx.RespondAsync(response);
                var userSelectionCtx = await interactivity.WaitForMessageAsync(m => m.Author.Id == ctx.Member.Id && int.TryParse(m.Content, out int a) && a <= i);
                if (userSelectionCtx != null && userSelectionCtx.Message.Content != "0")
                {
                    await ctx.RespondAsync(searchResults[Convert.ToInt32(userSelectionCtx.Message.Content) - 1].Url);
                }
                else if (userSelectionCtx.Message.Content == "0")
                {
                    await ctx.RespondAsync("Arama isteğiniz iptal edildi. (0 yazdığınız için)");
                }
                else
                {
                    await ctx.RespondAsync("Arama isteğiniz zaman aşımına uğradı. (1 dakika)");
                }
            }
            else
            {
                searchString = mainString;
                VideoInformation searchResult = items.SearchQuery(searchString, 1).FirstOrDefault();
                await ctx.RespondAsync(searchResult.Url);
                return;
            }


            // (x + y - 1) ÷ y =
            // x = itemCount    y = 19
           
        }

        [Command("yeniyıl")]
        public async Task NewYear(CommandContext ctx)
        {
            while (DateTime.Now.Minute != 59)
            {
                Console.WriteLine("Waiting 5 seconds");
                System.Threading.Thread.Sleep(5000);
            }
            while (DateTime.Now.Second < 54)
            {
                Console.WriteLine("Waiting half a second");
                System.Threading.Thread.Sleep(500);
            }
            await Play(ctx, "pezevenk");
        }

        [Command("purge")]
        public async Task Purge(CommandContext ctx)
        {
            var voiceNext = ctx.Client.GetVoiceNext();
            var voiceNextConnection = voiceNext.GetConnection(ctx.Guild);
            voiceNextConnection.Disconnect();
        }

        [Command("record")]
        public async Task RecordToggle(CommandContext ctx)
        {
            if (recordingDisabled)
            {
                recordingDisabled = false;
            }
            else
            {
                recordingDisabled = true;
            }
            await ctx.RespondAsync("Recording has been " + (recordingDisabled ? "disabled" : "enabled"));
        }

        public bool IsOwner(ulong UId, string audioName)
        {
            ProcessStartInfo ffprobeInfo = new ProcessStartInfo()
            {
                FileName = "ffprobe.exe",
                Arguments = "-i \"" + audioName + ".mp3\" -v error -of default=noprint_wrappers=1:nokey=1 -hide_banner -show_entries format_tags=author",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WorkingDirectory = @"..\..\res\"
            };
            Process ffprobe = Process.Start(ffprobeInfo);
            string UserId = "0";
            while (!ffprobe.StandardOutput.EndOfStream)
            {
                UserId = ffprobe.StandardOutput.ReadLine();
            }
            ffprobe.WaitForExit();
            ffprobe.Dispose();
            return UserId == UId.ToString();
        }

        public void Log(string logString)
        {
            File.AppendAllText(@"..\..\res\log.txt", Environment.NewLine + DateTime.Now.ToString() + " || " + logString);
        }

        public async Task OnVoiceReceived(VoiceReceiveEventArgs ea)
        {
            if (recordingDisabled)
                return;
            if (!this.ffmpegs.ContainsKey(ea.SSRC))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $@"-ac 2 -f s16le -ar 48000 -i pipe:0 -ac 2 -ar 44100 {ea.SSRC}.wav",
                    UseShellExecute = false,
                    RedirectStandardInput = true
                };

                this.ffmpegs.TryAdd(ea.SSRC, Process.Start(psi));
            }

            var buff = ea.Voice.ToArray();

            var ffmpeg = this.ffmpegs[ea.SSRC];
            await ffmpeg.StandardInput.BaseStream.WriteAsync(buff, 0, buff.Length);
            await ffmpeg.StandardInput.BaseStream.FlushAsync();
        }
    }
}
