using Discord;
using Discord.Commands;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class HubModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    [Command("status")]
    [Alias("stats")]
    [Summary("获取机器人环境状态。")]
    public async Task GetStatusAsync()
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;

        var builder = new EmbedBuilder
        {
            Color = Color.Gold,
        };

        var runner = SysCord<T>.Runner;
        var allBots = runner.Bots.ConvertAll(z => z.Bot);
        var botCount = allBots.Count;
        builder.AddField(x =>
        {
            x.Name = "概览";
            x.Value =
                $"机器人数量：{botCount}\n" +
                $"机器人状态：{SummarizeBots(allBots)}\n" +
                $"数据库内宝可梦数量：{hub.Ledy.Pool.Count}\n";
            x.IsInline = false;
        });

        builder.AddField(x =>
        {
            var bots = allBots.OfType<ICountBot>();
            var lines = bots.SelectMany(z => z.Counts.GetNonZeroCounts()).Distinct();
            var msg = string.Join("\n", lines);
            if (string.IsNullOrWhiteSpace(msg))
                msg = "尚无统计数据！";
            x.Name = "统计";
            x.Value = msg;
            x.IsInline = false;
        });

        var queues = hub.Queues.AllQueues;
        int count = 0;
        foreach (var q in queues)
        {
            var c = q.Count;
            if (c == 0)
                continue;

            var nextMsg = GetNextName(q);
            builder.AddField(x =>
            {
                x.Name = $"{q.Type} 队列";
                x.Value =
                    $"下一位：{nextMsg}\n" +
                    $"人数：{c}\n";
                x.IsInline = false;
            });
            count += c;
        }

        if (count == 0)
        {
            builder.AddField(x =>
            {
                x.Name = "队列为空";
                x.Value = "当前无人排队！";
                x.IsInline = false;
            });
        }

        await ReplyAsync("机器人状态", false, builder.Build()).ConfigureAwait(false);
    }

    private static string GetNextName(PokeTradeQueue<T> q)
    {
        var next = q.TryPeek(out var detail, out _);
        if (!next)
            return "无";

        var name = detail.Trainer.TrainerName;

        // show detail of trade if possible
        var nick = detail.TradeData.Nickname;
        if (!string.IsNullOrEmpty(nick))
            name += $" - {nick}";
        return name;
    }

    private static string SummarizeBots(IReadOnlyCollection<RoutineExecutor<PokeBotState>> bots)
    {
        if (bots.Count == 0)
            return "尚未配置机器人。";
        var summaries = bots.Select(z => $"- {z.GetSummary()}");
        return Environment.NewLine + string.Join(Environment.NewLine, summaries);
    }
}
