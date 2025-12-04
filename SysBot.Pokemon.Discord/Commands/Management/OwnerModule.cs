using AnimatedGif;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using PKHeX.Core;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord;

public class OwnerModule<T> : SudoModule<T> where T : PKM, new()
{
    [Command("listguilds")]
    [Alias("lg", "servers", "listservers")]
    [Summary("列出机器人当前所在的所有服务器。")]
    [RequireSudo]
    public async Task ListGuilds(int page = 1)
    {
        const int guildsPerPage = 25; // Discord limit for fields in an embed
        int guildCount = Context.Client.Guilds.Count;
        int totalPages = (int)Math.Ceiling(guildCount / (double)guildsPerPage);
        page = Math.Max(1, Math.Min(page, totalPages));

        var guilds = Context.Client.Guilds
            .Skip((page - 1) * guildsPerPage)
            .Take(guildsPerPage);

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"服务器列表 - 第 {page}/{totalPages} 页")
            .WithDescription("以下是我当前所在的服务器：")
            .WithColor((DiscordColor)Color.Blue);

        foreach (var guild in guilds)
        {
            embedBuilder.AddField(guild.Name, $"ID：{guild.Id}", inline: true);
        }
        var dmChannel = await Context.User.CreateDMChannelAsync();
        await dmChannel.SendMessageAsync(embed: embedBuilder.Build());

        await ReplyAsync($"{Context.User.Mention}，我已通过私信发送服务器列表（第 {page} 页）。");

        if (Context.Message is IUserMessage userMessage)
        {
            await Task.Delay(2000);
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
    }

    [Command("blacklistserver")]
    [Alias("bls")]
    [Summary("将指定服务器 ID 加入机器人黑名单。")]
    [RequireOwner]
    public async Task BlacklistServer(ulong serverId)
    {
        var settings = SysCord<T>.Runner.Hub.Config.Discord;

        if (settings.ServerBlacklist.Contains(serverId))
        {
            await ReplyAsync("该服务器已在黑名单中。");
            return;
        }

        var server = Context.Client.GetGuild(serverId);
        if (server == null)
        {
            await ReplyAsync("找不到该 ID 对应的服务器，请确认机器人在该服务器内。");
            return;
        }

        var newServerAccess = new RemoteControlAccess { ID = serverId, Name = server.Name, Comment = "已加入黑名单的服务器" };

        settings.ServerBlacklist.AddIfNew([newServerAccess]);

        await server.LeaveAsync();
        await ReplyAsync($"已退出服务器“{server.Name}”并将其加入黑名单。");
    }

    [Command("unblacklistserver")]
    [Alias("ubls")]
    [Summary("将指定服务器 ID 从机器人黑名单中移除。")]
    [RequireOwner]
    public async Task UnblacklistServer(ulong serverId)
    {
        var settings = SysCord<T>.Runner.Hub.Config.Discord;

        if (!settings.ServerBlacklist.Contains(serverId))
        {
            await ReplyAsync("该服务器不在黑名单中。");
            return;
        }

        var wasRemoved = settings.ServerBlacklist.RemoveAll(x => x.ID == serverId) > 0;

        if (wasRemoved)
        {
            await ReplyAsync($"已将 ID 为 {serverId} 的服务器移出黑名单。");
        }
        else
        {
            await ReplyAsync("移除黑名单时出现错误，请检查服务器 ID 后重试。");
        }
    }

    [Command("addSudo")]
    [Summary("将被提及的用户加入全局 sudo 列表。")]
    [RequireOwner]
    public async Task SudoUsers([Remainder] string _)
    {
        var users = Context.Message.MentionedUsers;
        var objects = users.Select(GetReference);
        SysCordSettings.Settings.GlobalSudoList.AddIfNew(objects);
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("removeSudo")]
    [Summary("将被提及的用户从全局 sudo 列表中移除。")]
    [RequireOwner]
    public async Task RemoveSudoUsers([Remainder] string _)
    {
        var users = Context.Message.MentionedUsers;
        var objects = users.Select(GetReference);
        SysCordSettings.Settings.GlobalSudoList.RemoveAll(z => objects.Any(o => o.ID == z.ID));
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("addChannel")]
    [Summary("将当前频道加入允许执行指令的白名单。")]
    [RequireOwner]
    public async Task AddChannel()
    {
        var obj = GetReference(Context.Message.Channel);
        SysCordSettings.Settings.ChannelWhitelist.AddIfNew([obj]);
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("syncChannels")]
    [Alias("sch", "syncchannels")]
    [Summary("将频道白名单中的所有频道同步到公告频道列表。")]
    [RequireOwner]
    public async Task SyncChannels()
    {
        var whitelist = SysCordSettings.Settings.ChannelWhitelist.List;
        var announcementList = SysCordSettings.Settings.AnnouncementChannels.List;

        bool changesMade = false;

        foreach (var channel in whitelist)
        {
            if (!announcementList.Any(x => x.ID == channel.ID))
            {
                announcementList.Add(channel);
                changesMade = true;
            }
        }

        if (changesMade)
        {
            await ReplyAsync("频道白名单已成功同步至公告频道列表。").ConfigureAwait(false);
        }
        else
        {
            await ReplyAsync("白名单中的频道均已在公告频道列表，无需修改。").ConfigureAwait(false);
        }
    }

    [Command("removeChannel")]
    [Summary("将当前频道从指令白名单中移除。")]
    [RequireOwner]
    public async Task RemoveChannel()
    {
        var obj = GetReference(Context.Message.Channel);
        SysCordSettings.Settings.ChannelWhitelist.RemoveAll(z => z.ID == obj.ID);
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("leave")]
    [Alias("bye")]
    [Summary("离开当前服务器。")]
    [RequireOwner]
    public async Task Leave()
    {
        await ReplyAsync("再见。").ConfigureAwait(false);
        await Context.Guild.LeaveAsync().ConfigureAwait(false);
    }

    [Command("leaveguild")]
    [Alias("lg")]
    [Summary("根据提供的 ID 退出对应服务器。")]
    [RequireOwner]
    public async Task LeaveGuild(string userInput)
    {
        if (!ulong.TryParse(userInput, out ulong id))
        {
            await ReplyAsync("请输入有效的服务器 ID。").ConfigureAwait(false);
            return;
        }

        var guild = Context.Client.Guilds.FirstOrDefault(x => x.Id == id);
        if (guild is null)
        {
            await ReplyAsync($"输入 ({userInput}) 不是有效的服务器 ID，或机器人不在该服务器。").ConfigureAwait(false);
            return;
        }

        await ReplyAsync($"正在离开 {guild}。").ConfigureAwait(false);
        await guild.LeaveAsync().ConfigureAwait(false);
    }

    [Command("leaveall")]
    [Summary("退出机器人当前所在的所有服务器。")]
    [RequireOwner]
    public async Task LeaveAll()
    {
        await ReplyAsync("正在离开所有服务器。").ConfigureAwait(false);
        foreach (var guild in Context.Client.Guilds)
        {
            await guild.LeaveAsync().ConfigureAwait(false);
        }
    }

    [Command("repeek")]
    [Alias("peek")]
    [Summary("截取当前配置的 Switch 屏幕并发送。")]
    [RequireSudo]
    public async Task RePeek()
    {
        string ip = OwnerModule<T>.GetBotIPFromJsonConfig();
        var source = new CancellationTokenSource();
        var token = source.Token;

        var bot = SysCord<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyAsync($"未找到 IP 为 {ip} 的机器人。").ConfigureAwait(false);
            return;
        }

        _ = Array.Empty<byte>();
        byte[]? bytes;
        try
        {
            bytes = await bot.Bot.Connection.PixelPeek(token).ConfigureAwait(false) ?? [];
        }
        catch (Exception ex)
        {
            await ReplyAsync($"获取截图数据时出错：{ex.Message}");
            return;
        }

        if (bytes.Length == 0)
        {
            await ReplyAsync("未收到任何截图数据。");
            return;
        }

        await using MemoryStream ms = new(bytes);
        const string img = "cap.jpg";
        var embed = new EmbedBuilder { ImageUrl = $"附件://{img}", Color = (DiscordColor?)Color.Purple }
            .WithFooter(new EmbedFooterBuilder { Text = "这是你的截图。" });

        await Context.Channel.SendFileAsync(ms, img, embed: embed.Build());
    }

    [Command("video")]
    [Alias("video")]
    [Summary("截取当前配置的 Switch 并生成 GIF。")]
    [RequireSudo]
    public async Task RePeekGIF()
    {
        await Context.Channel.SendMessageAsync("正在处理 GIF 请求…").ConfigureAwait(false);

        try
        {
            string ip = OwnerModule<T>.GetBotIPFromJsonConfig();
            var source = new CancellationTokenSource();
            var token = source.Token;
            var bot = SysCord<T>.Runner.GetBot(ip);

            if (bot == null)
            {
                await ReplyAsync($"未找到 IP 为 {ip} 的机器人。").ConfigureAwait(false);
                return;
            }

            const int screenshotCount = 10;
            var screenshotInterval = TimeSpan.FromSeconds(0.1 / 10);
            var gifFrames = new List<byte[]>();

            for (int i = 0; i < screenshotCount; i++)
            {
                byte[] bytes;
                try
                {
                    bytes = await bot.Bot.Connection.PixelPeek(token).ConfigureAwait(false) ?? Array.Empty<byte>();
                }
                catch (Exception ex)
                {
                    await ReplyAsync($"获取截图数据时出错：{ex.Message}").ConfigureAwait(false);
                    return;
                }

                if (bytes.Length == 0)
                {
                    await ReplyAsync("未收到任何截图数据。").ConfigureAwait(false);
                    return;
                }

                gifFrames.Add(bytes);

                if (i < screenshotCount - 1)
                {
                    await Task.Delay(screenshotInterval).ConfigureAwait(false);
                }
            }

            await using (var ms = new MemoryStream())
            {
                await CreateGifAsync(ms, gifFrames).ConfigureAwait(false);

                ms.Position = 0;
                const string gifFileName = "screenshot.gif";
                var embed = new EmbedBuilder { ImageUrl = $"附件://{gifFileName}", Color = (DiscordColor?)Color.Red }
                    .WithFooter(new EmbedFooterBuilder { Text = "这是你的 GIF。" });

                await Context.Channel.SendFileAsync(ms, gifFileName, embed: embed.Build()).ConfigureAwait(false);
            }

            gifFrames.Clear();
        }
        catch (Exception ex)
        {
            await ReplyAsync($"处理 GIF 时出错：{ex.Message}").ConfigureAwait(false);
        }
    }

    private async Task CreateGifAsync(Stream outputStream, List<byte[]> frames)
    {
#pragma warning disable CA1416 // Validate platform compatibility
        using var gif = new AnimatedGifCreator(outputStream, 200);
        foreach (var frameBytes in frames)
        {
            using (var ms = new MemoryStream(frameBytes))
            using (var bitmap = new Bitmap(ms))
            using (var frame = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                gif.AddFrame(frame);
            }
            await Task.Yield(); // Allow other tasks to run
        }
#pragma warning restore CA1416 // Validate platform compatibility
    }

    private static string GetBotIPFromJsonConfig()
    {
        try
        {
            var jsonData = File.ReadAllText(PokeBot.ConfigPath);
            var config = JObject.Parse(jsonData);

            var botsArray = config["Bots"] as JArray;
            if (botsArray == null || botsArray.Count == 0)
                return "192.168.1.1";
            
            var firstBot = botsArray[0] as JObject;
            var connection = firstBot?["Connection"] as JObject;
            var ip = connection?["IP"]?.ToString();
            
            return ip ?? "192.168.1.1";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取配置文件时出错：{ex.Message}");
            return "192.168.1.1";
        }
    }

    [Command("kill")]
    [Alias("shutdown")]
    [Summary("立即终止整个进程。")]
    [RequireOwner]
    public async Task ExitProgram()
    {
        await Context.Channel.EchoAndReply("正在关闭……再见！**机器人服务即将下线。**").ConfigureAwait(false);
        Environment.Exit(0);
    }

    [Command("dm")]
    [Summary("向指定用户发送私信。")]
    [RequireOwner]
    public async Task DMUserAsync(SocketUser user, [Remainder] string message)
    {
        var attachments = Context.Message.Attachments;
        var hasAttachments = attachments.Count != 0;

        var embed = new EmbedBuilder
        {
            Title = "来自机器人所有者的私信",
            Description = message,
            Color = (DiscordColor?)Color.Gold,
            Timestamp = DateTimeOffset.Now,
            ThumbnailUrl = "https://raw.githubusercontent.com/hexbyt3/sprites/main/pikamail.png"
        };

        try
        {
            var dmChannel = await user.CreateDMChannelAsync();

            if (hasAttachments)
            {
                foreach (var attachment in attachments)
                {
                    using var httpClient = new HttpClient();
                    var stream = await httpClient.GetStreamAsync(attachment.Url);
                    var file = new FileAttachment(stream, attachment.Filename);
                    await dmChannel.SendFileAsync(file, embed: embed.Build());
                }
            }
            else
            {
                await dmChannel.SendMessageAsync(embed: embed.Build());
            }

            var confirmationMessage = await ReplyAsync($"已成功向 {user.Username} 发送私信。");
            await Context.Message.DeleteAsync();
            await Task.Delay(TimeSpan.FromSeconds(10));
            await confirmationMessage.DeleteAsync();
        }
        catch (Exception ex)
        {
            await ReplyAsync($"向 {user.Username} 发送私信失败：{ex.Message}");
        }
    }

    [Command("say")]
    [Summary("向指定频道发送消息。")]
    [RequireSudo]
    public async Task SayAsync([Remainder] string message)
    {
        var attachments = Context.Message.Attachments;
        var hasAttachments = attachments.Count != 0;

        var indexOfChannelMentionStart = message.LastIndexOf('<');
        var indexOfChannelMentionEnd = message.LastIndexOf('>');
        if (indexOfChannelMentionStart == -1 || indexOfChannelMentionEnd == -1)
        {
            await ReplyAsync("请使用 #频道 的格式正确提及频道。");
            return;
        }

        var channelMention = message.Substring(indexOfChannelMentionStart, indexOfChannelMentionEnd - indexOfChannelMentionStart + 1);
        var actualMessage = message.Substring(0, indexOfChannelMentionStart).TrimEnd();

        var channel = Context.Guild.Channels.FirstOrDefault(c => $"<#{c.Id}>" == channelMention);

        if (channel == null)
        {
            await ReplyAsync("找不到该频道。");
            return;
        }

        if (channel is not IMessageChannel messageChannel)
        {
            await ReplyAsync("提及的频道不是文字频道。");
            return;
        }

        // If there are attachments, send them to the channel
        if (hasAttachments)
        {
            foreach (var attachment in attachments)
            {
                using var httpClient = new HttpClient();
                var stream = await httpClient.GetStreamAsync(attachment.Url);
                var file = new FileAttachment(stream, attachment.Filename);
                await messageChannel.SendFileAsync(file, actualMessage);
            }
        }
        else
        {
            await messageChannel.SendMessageAsync(actualMessage);
        }

        // Send confirmation message to the user
        await ReplyAsync($"已成功在 {channelMention} 发送消息。");
    }

    private RemoteControlAccess GetReference(IUser channel) => new()
    {
        ID = channel.Id,
        Name = channel.Username,
        Comment = $"{Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd.hh\\:mm\\:ss} 添加",
    };

    private RemoteControlAccess GetReference(IChannel channel) => new()
    {
        ID = channel.Id,
        Name = channel.Name,
        Comment = $"{Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd.hh\\:mm\\:ss} 添加",
    };
}
