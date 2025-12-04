using Discord;
using Discord.Commands;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class RecoveryModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static IPokeBotRunner? Runner => SysCord<T>.Runner;

    [Command("recovery")]
    [Alias("recover")]
    [Summary("查看所有机器人的恢复状态。")]
    [RequireSudo]
    public async Task ShowRecoveryStatusAsync()
    {
        if (Runner == null)
        {
            await ReplyAsync("机器人运行器尚未初始化。").ConfigureAwait(false);
            return;
        }

        if (Runner is not PokeBotRunner<T> runner)
        {
            await ReplyAsync("该类型的运行器不支持恢复服务。").ConfigureAwait(false);
            return;
        }
        
        var recoveryService = runner.GetRecoveryService();
        
        if (recoveryService == null)
        {
            await ReplyAsync("恢复服务未启用。").ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("机器人恢复状态")
            .WithColor(Color.Blue)
            .WithTimestamp(DateTimeOffset.Now);

        var hasRecoveryData = false;
        foreach (var bot in Runner.Bots)
        {
            var state = bot.GetRecoveryState();
            if (state != null && (state.ConsecutiveFailures > 0 || state.CrashHistory.Count > 0))
            {
                hasRecoveryData = true;
                var status = bot.IsRunning ? "🟢 运行中" : "🔴 已停止";
                if (state.IsRecovering)
                    status = "🟠 恢复中";

                var fieldValue = $"状态：{status}\n" +
                                $"崩溃次数：{state.CrashHistory.Count}\n" +
                                $"连续失败：{state.ConsecutiveFailures}";
                
                if (state.LastRecoveryAttempt.HasValue)
                {
                    fieldValue += $"\n上次恢复：{state.LastRecoveryAttempt.Value:HH:mm:ss}";
                }
                
                embed.AddField(bot.Bot.Connection.Name, fieldValue, true);
            }
        }

        if (!hasRecoveryData)
        {
            embed.WithDescription("所有机器人运行正常，暂无恢复记录。");
        }

        await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    [Command("recoveryReset")]
    [Alias("resetRecovery")]
    [Summary("重置指定机器人的恢复状态。")]
    [RequireSudo]
    public async Task ResetRecoveryAsync([Remainder] string botName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(botName);
        
        if (Runner == null)
        {
            await ReplyAsync("机器人运行器尚未初始化。").ConfigureAwait(false);
            return;
        }

        if (Runner is not PokeBotRunner<T> runner)
        {
            await ReplyAsync("该类型的运行器不支持恢复服务。").ConfigureAwait(false);
            return;
        }
        
        var recoveryService = runner.GetRecoveryService();
        
        if (recoveryService == null)
        {
            await ReplyAsync("恢复服务未启用。").ConfigureAwait(false);
            return;
        }

        var bot = Runner.Bots.FirstOrDefault(b => b.Bot.Connection.Name.Equals(botName, StringComparison.OrdinalIgnoreCase));
        if (bot == null)
        {
            await ReplyAsync($"未找到名为“{botName}”的机器人。").ConfigureAwait(false);
            return;
        }

        recoveryService.ResetRecoveryState(bot.Bot.Connection.Name);
        await ReplyAsync($"机器人“{bot.Bot.Connection.Name}”的恢复状态已重置。").ConfigureAwait(false);
    }

    [Command("recoveryToggle")]
    [Alias("toggleRecovery")]
    [Summary("启用或停用恢复系统。")]
    [RequireSudo]
    public async Task ToggleRecoveryAsync()
    {
        if (Runner == null)
        {
            await ReplyAsync("机器人运行器尚未初始化。").ConfigureAwait(false);
            return;
        }

        if (Runner is not PokeBotRunner<T> runner)
        {
            await ReplyAsync("该类型的运行器不支持恢复服务。").ConfigureAwait(false);
            return;
        }
        
        var config = Runner.Config.Recovery;
        config.EnableRecovery = !config.EnableRecovery;

        var status = config.EnableRecovery ? "已启用" : "已停用";
        await ReplyAsync($"恢复系统：{status}。").ConfigureAwait(false);
        
        // Update the recovery service state
        if (config.EnableRecovery)
            runner.RecoveryService?.EnableRecovery();
        else
            runner.RecoveryService?.DisableRecovery();
    }

    [Command("recoveryConfig")]
    [Alias("recoveryCfg")]
    [Summary("查看当前恢复配置。")]
    [RequireSudo]
    public async Task ShowRecoveryConfigAsync()
    {
        if (Runner == null)
        {
            await ReplyAsync("机器人运行器尚未初始化。").ConfigureAwait(false);
            return;
        }

        var config = Runner.Config.Recovery;
        
        var embed = new EmbedBuilder()
            .WithTitle("恢复配置")
            .WithColor(Color.Blue)
            .WithTimestamp(DateTimeOffset.Now)
            .AddField("是否启用", config.EnableRecovery ? "✅ 是" : "❌ 否", true)
            .AddField("最大尝试次数", config.MaxRecoveryAttempts, true)
            .AddField("初始延迟", $"{config.InitialRecoveryDelaySeconds}s", true)
            .AddField("最大延迟", $"{config.MaxRecoveryDelaySeconds}s", true)
            .AddField("退避倍率", $"{config.BackoffMultiplier}x", true)
            .AddField("崩溃统计窗口", $"{config.CrashHistoryWindowMinutes} 分钟", true)
            .AddField("窗口内最大崩溃", config.MaxCrashesInWindow, true)
            .AddField("恢复主动停止", config.RecoverIntentionalStops ? "✅" : "❌", true)
            .AddField("最小稳定运行", $"{config.MinimumStableUptimeSeconds}s", true);

        await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
    }
}