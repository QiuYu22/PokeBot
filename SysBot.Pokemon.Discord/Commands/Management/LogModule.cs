using Discord;
using Discord.Commands;
using Discord.WebSocket;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class LogModule : ModuleBase<SocketCommandContext>
{
    private static readonly Dictionary<ulong, ChannelLogger> Channels = [];

    public static void RestoreLogging(DiscordSocketClient discord, DiscordSettings settings)
    {
        foreach (var ch in settings.LoggingChannels)
        {
            if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                AddLogChannel(c, ch.ID);
        }

        LogUtil.LogInfo("Discord", "机器人启动时已在 Discord 频道启用日志输出。");
    }

    [Command("logHere")]
    [Summary("让机器人在当前频道输出日志。")]
    [RequireSudo]
    public async Task AddLogAsync()
    {
        var c = Context.Channel;
        var cid = c.Id;
        if (Channels.TryGetValue(cid, out _))
        {
            await ReplyAsync("当前频道已启用日志输出。").ConfigureAwait(false);
            return;
        }

        AddLogChannel(c, cid);

        // Add to discord global loggers (saves on program close)
        SysCordSettings.Settings.LoggingChannels.AddIfNew([GetReference(Context.Channel)]);
        await ReplyAsync("已在该频道启用日志输出！").ConfigureAwait(false);
    }

    [Command("logClearAll")]
    [Summary("清除所有频道的日志设置。")]
    [RequireSudo]
    public async Task ClearLogsAllAsync()
    {
        foreach (var l in Channels)
        {
            var entry = l.Value;
            await ReplyAsync($"已从 {entry.ChannelName}（{entry.ChannelID}）清除日志设置。").ConfigureAwait(false);
            LogUtil.Forwarders.Remove(entry);
        }

        LogUtil.Forwarders.RemoveAll(y => Channels.Select(z => z.Value).Contains(y));
        Channels.Clear();
        SysCordSettings.Settings.LoggingChannels.Clear();
        await ReplyAsync("已清除所有频道的日志设置！").ConfigureAwait(false);
    }

    [Command("logClear")]
    [Summary("清除当前频道的日志设置。")]
    [RequireSudo]
    public async Task ClearLogsAsync()
    {
        var id = Context.Channel.Id;
        if (!Channels.TryGetValue(id, out var log))
        {
            await ReplyAsync("该频道未启用日志输出。").ConfigureAwait(false);
            return;
        }
        LogUtil.Forwarders.Remove(log);
        Channels.Remove(Context.Channel.Id);
        SysCordSettings.Settings.LoggingChannels.RemoveAll(z => z.ID == id);
        await ReplyAsync($"已从频道 {Context.Channel.Name} 清除日志设置。").ConfigureAwait(false);
    }

    [Command("logInfo")]
    [Summary("列出当前的日志设置。")]
    [RequireSudo]
    public async Task DumpLogInfoAsync()
    {
        foreach (var c in Channels)
            await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
    }

    private static void AddLogChannel(ISocketMessageChannel c, ulong cid)
    {
        var logger = new ChannelLogger(cid, c);
        LogUtil.Forwarders.Add(logger);
        Channels.Add(cid, logger);
    }

    private RemoteControlAccess GetReference(IChannel channel) => new()
    {
        ID = channel.Id,
        Name = channel.Name,
        Comment = $"由 {Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd-hh:mm:ss} 添加",
    };
}
