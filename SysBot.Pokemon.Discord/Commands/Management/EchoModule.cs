using Discord;
using Discord.Commands;
using Discord.WebSocket;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static SysBot.Pokemon.DiscordSettings;

namespace SysBot.Pokemon.Discord
{
    public static class EmbedColorConverter
    {
        public static Color ToDiscordColor(this EmbedColorOption colorOption)
        {
            return colorOption switch
            {
                EmbedColorOption.Blue => Color.Blue,
                EmbedColorOption.Green => Color.Green,
                EmbedColorOption.Red => Color.Red,
                EmbedColorOption.Gold => Color.Gold,
                EmbedColorOption.Purple => Color.Purple,
                EmbedColorOption.Teal => Color.Teal,
                EmbedColorOption.Orange => Color.Orange,
                EmbedColorOption.Magenta => Color.Magenta,
                EmbedColorOption.LightGrey => Color.LightGrey,
                EmbedColorOption.DarkGrey => Color.DarkGrey,
                _ => Color.Blue,  // Default to Blue if somehow an undefined enum value is used
            };
        }
    }

    public class EchoModule : ModuleBase<SocketCommandContext>
    {
        private static DiscordSettings? Settings { get; set; }

        private static DiscordSocketClient? _discordClient;

        private class EchoChannel(ulong channelId, string channelName, Action<string> action, Action<byte[], string, EmbedBuilder> raidAction)
        {
            public readonly ulong ChannelID = channelId;

            public readonly string ChannelName = channelName;

            public readonly Action<string> Action = action;

            public readonly Action<byte[], string, EmbedBuilder> RaidAction = raidAction;

            public string EmbedResult = string.Empty;
        }

        private class EncounterEchoChannel(ulong channelId, string channelName, Action<string, Embed> embedaction)
        {
            public readonly ulong ChannelID = channelId;

            public readonly string ChannelName = channelName;

            public readonly Action<string, Embed> EmbedAction = embedaction;

            public string EmbedResult = string.Empty;
        }

        private static readonly Dictionary<ulong, EchoChannel> Channels = [];

        private static readonly Dictionary<ulong, EncounterEchoChannel> EncounterChannels = [];

        public static void RestoreChannels(DiscordSocketClient discord, DiscordSettings cfg)
        {
            Settings = cfg;
            _discordClient = discord;
            foreach (var ch in cfg.AnnouncementChannels)
            {
                if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                    AddEchoChannel(c, ch.ID);
            }

            // EchoUtil.Echo("Added echo notification to Discord channel(s) on Bot startup.");
        }

        public static async Task SendQueueStatusEmbedAsync(bool isFull, int currentCount, int maxCount)
        {
            if (Settings == null || _discordClient == null || Channels.Count == 0)
                return;

            var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var formattedTimestamp = $"<t:{unixTimestamp}:F>";

            var embedColor = isFull ? Color.Red : Color.Green;
            var title = isFull ? "🚫 队列已满！" : "✅ 队列已开放！";
            var description = isFull
                ? $"队列已达到上限，现已 **关闭**。\n\n**当前队列人数：** {currentCount}/{maxCount}\n\n当完成部分交易后将自动重新开放。\n\n**状态更新时间：** {formattedTimestamp}"
                : $"队列现已 **开放**，欢迎继续排队！\n\n**当前队列人数：** {currentCount}/{maxCount}\n\n**状态更新时间：** {formattedTimestamp}";

            var thumbnailUrl = Settings.AnnouncementSettings.RandomAnnouncementThumbnail ? GetRandomThumbnail() : GetSelectedThumbnail();

            var embed = new EmbedBuilder
            {
                Color = embedColor,
                Description = description
            }
            .WithTitle(title)
            .WithThumbnailUrl(thumbnailUrl)
            .WithFooter("队列状态自动更新")
            .Build();

            foreach (var channelEntry in Channels)
            {
                var channelId = channelEntry.Key;
                try
                {
                    if (_discordClient.GetChannel(channelId) is ISocketMessageChannel channel)
                    {
                        await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"向频道 {channelId} 发送队列状态失败：{ex.Message}", nameof(SendQueueStatusEmbedAsync));
                }
            }
        }

        [Command("Announce", RunMode = RunMode.Async)]
        [Alias("announce")]
        [Summary("向所有已添加回显（Echo）的频道发送公告。")]
        [RequireOwner]
        public async Task AnnounceAsync([Remainder] string announcement)
        {
            var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var formattedTimestamp = $"<t:{unixTimestamp}:F>";
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            var embedColor = Settings.AnnouncementSettings.RandomAnnouncementColor ? GetRandomColor() : Settings.AnnouncementSettings.AnnouncementEmbedColor.ToDiscordColor();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            var thumbnailUrl = Settings.AnnouncementSettings.RandomAnnouncementThumbnail ? GetRandomThumbnail() : GetSelectedThumbnail();

            var embedDescription = $"## {announcement}\n\n**发送时间：{formattedTimestamp}**";

            var embed = new EmbedBuilder
            {
                Color = embedColor,
                Description = embedDescription
            }
            .WithTitle("重要公告！")
            .WithThumbnailUrl(thumbnailUrl)
            .Build();

            var client = Context.Client;
            foreach (var channelEntry in Channels)
            {
                var channelId = channelEntry.Key;
                if (client.GetChannel(channelId) is not ISocketMessageChannel channel)
                {
                    LogUtil.LogError($"无法找到或访问频道 {channelId}", nameof(AnnounceAsync));
                    continue;
                }

                try
                {
                    await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"向频道 {channel.Name} 发送公告失败：{ex.Message}", nameof(AnnounceAsync));
                }
            }
            var confirmationMessage = await ReplyAsync("公告已发送至所有 Echo 频道。").ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            await confirmationMessage.DeleteAsync().ConfigureAwait(false);
            await Context.Message.DeleteAsync().ConfigureAwait(false);
        }

        private static Color GetRandomColor()
        {
            var random = new Random();
            var colors = Enum.GetValues(typeof(EmbedColorOption)).Cast<EmbedColorOption>().ToList();
            return colors[random.Next(colors.Count)].ToDiscordColor();
        }

        private static string GetRandomThumbnail()
        {
            var thumbnailOptions = new List<string>
    {
        "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/gengarmegaphone.png",
        "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/pikachumegaphone.png",
        "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/umbreonmegaphone.png",
        "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/sylveonmegaphone.png",
        "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/charmandermegaphone.png",
        "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/jigglypuffmegaphone.png",
        "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/flareonmegaphone.png",
    };
            var random = new Random();
            return thumbnailOptions[random.Next(thumbnailOptions.Count)];
        }

        private static string GetSelectedThumbnail()
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            if (!string.IsNullOrEmpty(Settings.AnnouncementSettings.CustomAnnouncementThumbnailUrl))
            {
                return Settings.AnnouncementSettings.CustomAnnouncementThumbnailUrl;
            }
            else
            {
                return GetUrlFromThumbnailOption(Settings.AnnouncementSettings.AnnouncementThumbnailOption);
            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        private static string GetUrlFromThumbnailOption(ThumbnailOption option)
        {
            return option switch
            {
                ThumbnailOption.Gengar => "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/gengarmegaphone.png",
                ThumbnailOption.Pikachu => "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/pikachumegaphone.png",
                ThumbnailOption.Umbreon => "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/umbreonmegaphone.png",
                ThumbnailOption.Sylveon => "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/sylveonmegaphone.png",
                ThumbnailOption.Charmander => "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/charmandermegaphone.png",
                ThumbnailOption.Jigglypuff => "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/jigglypuffmegaphone.png",
                ThumbnailOption.Flareon => "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/flareonmegaphone.png",
                _ => "https://raw.githubusercontent.com/hexbyt3/sprites/main/imgs/gengarmegaphone.png",
            };
        }

        [Command("addEmbedChannel")]
        [Alias("aec")]
        [Summary("让机器人在该频道发布团战嵌入。")]
        [RequireSudo]
        public async Task AddEchoAsync()
        {
            var c = Context.Channel;
            var cid = c.Id;
            if (Channels.TryGetValue(cid, out _))
            {
                await ReplyAsync("此频道已启用通知。").ConfigureAwait(false);
                return;
            }

            AddEchoChannel(c, cid);

            SysCordSettings.Settings.AnnouncementChannels.AddIfNew([GetReference(Context.Channel)]);
            await ReplyAsync("已在此频道添加交易嵌入输出！").ConfigureAwait(false);
        }

        private static async Task<bool> SendMessageWithRetry(ISocketMessageChannel c, string message, int maxRetries = 3)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    await c.SendMessageAsync(message).ConfigureAwait(false);
                    return true; // Successfully sent the message, exit the loop.
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"向频道 '{c.Name}' 发送消息失败（尝试 {retryCount + 1}）：{ex.Message}", nameof(AddEchoChannel));
                    retryCount++;
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false); // Wait for 5 seconds before retrying.
                }
            }
            return false; // Reached max number of retries without success.
        }

        private static async Task<bool> RaidEmbedAsync(ISocketMessageChannel c, byte[] bytes, string fileName, EmbedBuilder embed, int maxRetries = 2)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    if (bytes is not null && bytes.Length > 0)
                    {
                        await c.SendFileAsync(new MemoryStream(bytes), fileName, "", false, embed: embed.Build()).ConfigureAwait(false);
                    }
                    else
                    {
                        await c.SendMessageAsync("", false, embed.Build()).ConfigureAwait(false);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"向频道 '{c.Name}' 发送嵌入失败（尝试 {retryCount + 1}）：{ex.Message}", nameof(AddEchoChannel));
                    retryCount++;
                    if (retryCount < maxRetries)
                        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false); // Wait for a second before retrying.
                }
            }
            return false;
        }

        private static void AddEchoChannel(ISocketMessageChannel c, ulong cid)
        {
            async void l(string msg) => await SendMessageWithRetry(c, msg).ConfigureAwait(false);
            async void rb(byte[] bytes, string fileName, EmbedBuilder embed) => await RaidEmbedAsync(c, bytes, fileName, embed).ConfigureAwait(false);

            EchoUtil.Forwarders.Add(l);
            var entry = new EchoChannel(cid, c.Name, l, rb);
            Channels.Add(cid, entry);
        }

        public static bool IsEchoChannel(ISocketMessageChannel c)
        {
            var cid = c.Id;
            return Channels.TryGetValue(cid, out _);
        }

        public static bool IsEmbedEchoChannel(ISocketMessageChannel c)
        {
            var cid = c.Id;
            return EncounterChannels.TryGetValue(cid, out _);
        }

        [Command("echoInfo")]
        [Summary("列出特殊消息回显（Echo）频道设置。")]
        [RequireSudo]
        public async Task DumpEchoInfoAsync()
        {
            foreach (var c in Channels)
                await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
        }

        [Command("echoClear")]
        [Alias("rec")]
        [Summary("清除此频道的回显（Echo）设置。")]
        [RequireSudo]
        public async Task ClearEchosAsync()
        {
            var id = Context.Channel.Id;
            if (!Channels.TryGetValue(id, out var echo))
            {
                await ReplyAsync("此频道尚未启用回显（Echo）。").ConfigureAwait(false);
                return;
            }
            EchoUtil.Forwarders.Remove(echo.Action);
            Channels.Remove(Context.Channel.Id);
            SysCordSettings.Settings.AnnouncementChannels.RemoveAll(z => z.ID == id);
            await ReplyAsync($"已清除频道 {Context.Channel.Name} 的回显（Echo）设置。").ConfigureAwait(false);
        }

        [Command("echoClearAll")]
        [Alias("raec")]
        [Summary("清除所有频道的回显（Echo）设置。")]
        [RequireSudo]
        public async Task ClearEchosAllAsync()
        {
            foreach (var l in Channels)
            {
                var entry = l.Value;
                await ReplyAsync($"已清除频道 {entry.ChannelName} ({entry.ChannelID}) 的回显（Echo）设置。").ConfigureAwait(false);
                EchoUtil.Forwarders.Remove(entry.Action);
            }
            EchoUtil.Forwarders.RemoveAll(y => Channels.Select(x => x.Value.Action).Contains(y));
            Channels.Clear();
            SysCordSettings.Settings.AnnouncementChannels.Clear();
            await ReplyAsync("已清除所有频道的回显（Echo）设置！").ConfigureAwait(false);
        }

        private RemoteControlAccess GetReference(IChannel channel) => new()
        {
            ID = channel.Id,
            Name = channel.Name,
            Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
        };
    }
}
