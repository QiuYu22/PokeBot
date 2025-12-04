using Discord;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using PKHeX.Core;
using SysBot.Pokemon.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class BotModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        [Command("botStatus")]
        [Summary("获取所有机器人的状态。")]
        [RequireSudo]
        public async Task GetStatusAsync()
        {
            var me = SysCord<T>.Runner;
            var bots = me.Bots.Select(z => z.Bot).OfType<PokeRoutineExecutorBase>().ToArray();
            if (bots.Length == 0)
            {
                await ReplyAsync("尚未配置任何机器人。").ConfigureAwait(false);
                return;
            }

            var summaries = bots.Select(GetDetailedSummary);
            var lines = string.Join(Environment.NewLine, summaries);
            await ReplyAsync(Format.Code(lines)).ConfigureAwait(false);
        }

        private static string GetBotIPFromJsonConfig()
        {
            try
            {
                // Read the file and parse the JSON
                var jsonData = File.ReadAllText(PokeBot.ConfigPath);
                var config = JObject.Parse(jsonData);

                // Access the IP address from the first bot in the Bots array
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                var ip = config["Bots"][0]["Connection"]["IP"].ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                return ip;
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during reading or parsing the file
                Console.WriteLine($"读取配置文件时出错：{ex.Message}");
                return "192.168.1.1"; // Default IP if error occurs
            }
        }

        private static string GetDetailedSummary(PokeRoutineExecutorBase z)
        {
            return $"- {z.Connection.Name} | {z.Connection.Label} - {z.Config.CurrentRoutineType} ~ {z.LastTime:hh:mm:ss} | {z.LastLogged}";
        }

        [Command("botStart")]
        [Summary("启动指定（或默认）的机器人。")]
        [RequireSudo]
        public async Task StartBotAsync([Summary("机器人的 IP 地址")] string? ip = null)
        {
            if (ip == null)
                ip = BotModule<T>.GetBotIPFromJsonConfig();

            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"未找到 IP 为 {ip} 的机器人。").ConfigureAwait(false);
                return;
            }

            bot.Start();
            await ReplyAsync("机器人已启动。").ConfigureAwait(false);
        }

        [Command("botStop")]
        [Summary("停止指定（或默认）的机器人。")]
        [RequireSudo]
        public async Task StopBotAsync([Summary("机器人的 IP 地址")] string? ip = null)
        {
            if (ip == null)
                ip = BotModule<T>.GetBotIPFromJsonConfig();

            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"未找到 IP 为 {ip} 的机器人。").ConfigureAwait(false);
                return;
            }

            bot.Stop();
            await ReplyAsync("机器人已停止。").ConfigureAwait(false);
        }

        [Command("botIdle")]
        [Alias("botPause")]
        [Summary("让指定（或默认）的机器人进入空闲状态。")]
        [RequireSudo]
        public async Task IdleBotAsync([Summary("机器人的 IP 地址")] string? ip = null)
        {
            if (ip == null)
                ip = BotModule<T>.GetBotIPFromJsonConfig();

            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"未找到 IP 为 {ip} 的机器人。").ConfigureAwait(false);
                return;
            }

            bot.Pause();
            await ReplyAsync("机器人已进入空闲状态。").ConfigureAwait(false);
        }

        [Command("botChange")]
        [Summary("修改指定（或默认）机器人的当前例程（例如交易类型）。")]
        [RequireSudo]
        public async Task ChangeTaskAsync([Summary("例程枚举名")] PokeRoutineType task, [Summary("机器人的 IP 地址")] string? ip = null)
        {
            if (ip == null)
                ip = BotModule<T>.GetBotIPFromJsonConfig();

            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"未找到 IP 为 {ip} 的机器人。").ConfigureAwait(false);
                return;
            }

            bot.Bot.Config.Initialize(task);
            await ReplyAsync($"机器人已切换到 {task} 例程。").ConfigureAwait(false);
        }

        [Command("botRestart")]
        [Summary("重启指定（或默认）的机器人。")]
        [RequireSudo]
        public async Task RestartBotAsync([Summary("机器人的 IP 地址")] string? ip = null)
        {
            if (ip == null)
                ip = BotModule<T>.GetBotIPFromJsonConfig();

            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"未找到 IP 为 {ip} 的机器人。").ConfigureAwait(false);
                return;
            }

            var c = bot.Bot.Connection;
            c.Reset();
            bot.Start();
            await ReplyAsync("机器人已重启。").ConfigureAwait(false);
        }
    }
}
