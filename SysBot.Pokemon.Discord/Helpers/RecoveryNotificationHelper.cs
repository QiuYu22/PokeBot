using Discord;
using Discord.WebSocket;
using SysBot.Base;
using System;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

/// <summary>
/// Helper class for sending bot recovery notifications to Discord.
/// </summary>
public static class RecoveryNotificationHelper
{
    private static DiscordSocketClient? _client;
    private static ulong? _notificationChannelId;
    private static string _hubName = "机器人枢纽";

    /// <summary>
    /// Initializes the recovery notification system with Discord client and channel.
    /// </summary>
    public static void Initialize(DiscordSocketClient client, ulong? notificationChannelId, string hubName)
    {
        _client = client;
        _notificationChannelId = notificationChannelId;
        _hubName = hubName;
    }

    /// <summary>
    /// Hooks up recovery events to Discord notifications.
    /// </summary>
    public static void HookRecoveryEvents<T>(BotRecoveryService<T> recoveryService) where T : class, IConsoleBotConfig
    {
        if (recoveryService == null || _client == null)
            return;

        recoveryService.BotCrashed += async (sender, e) => await OnBotCrashed(e);
        recoveryService.RecoveryAttempted += async (sender, e) => await OnRecoveryAttempted(e);
        recoveryService.RecoverySucceeded += async (sender, e) => await OnRecoverySucceeded(e);
        recoveryService.RecoveryFailed += async (sender, e) => await OnRecoveryFailed(e);
    }

    private static async Task OnBotCrashed(BotCrashEventArgs e)
    {
        var embed = new EmbedBuilder()
            .WithTitle("⚠️ 检测到机器人崩溃")
            .WithDescription($"**机器人：** {e.BotName}\n**时间：** {e.CrashTime:yyyy-MM-dd HH:mm:ss} UTC")
            .WithColor(Color.Orange)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField("状态", "正在尝试自动恢复…", false)
            .WithFooter($"{_hubName} 恢复系统")
            .Build();

        await SendNotificationAsync(embed);
    }

    private static async Task OnRecoveryAttempted(BotRecoveryEventArgs e)
    {
        if (!e.IsSuccess) // Only notify on attempts, not successes (handled separately)
        {
            var embed = new EmbedBuilder()
                .WithTitle("🔄 正在尝试恢复")
                .WithDescription($"**机器人：** {e.BotName}\n**第 {e.AttemptNumber} 次尝试**")
                .WithColor(Color.Blue)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithFooter($"{_hubName} 恢复系统")
                .Build();

            await SendNotificationAsync(embed);
        }
    }

    private static async Task OnRecoverySucceeded(BotRecoveryEventArgs e)
    {
        var embed = new EmbedBuilder()
            .WithTitle("✅ 机器人恢复成功")
            .WithDescription($"**机器人：** {e.BotName}\n**尝试次数：** {e.AttemptNumber}")
            .WithColor(Color.Green)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField("状态", "机器人已恢复正常运行", false)
            .WithFooter($"{_hubName} 恢复系统")
            .Build();

        await SendNotificationAsync(embed);
    }

    private static async Task OnRecoveryFailed(BotRecoveryEventArgs e)
    {
        var embed = new EmbedBuilder()
            .WithTitle("❌ 机器人恢复失败")
            .WithDescription($"**机器人：** {e.BotName}\n**尝试次数：** {e.AttemptNumber}")
            .WithColor(Color.Red)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField("原因", e.FailureReason ?? "未知错误", false)
            .AddField("需采取的行动", "需要人工干预以重新启动该机器人", false)
            .WithFooter($"{_hubName} 恢复系统")
            .Build();

        await SendNotificationAsync(embed);
    }

    private static async Task SendNotificationAsync(Embed embed)
    {
        try
        {
            if (_client == null || !_notificationChannelId.HasValue)
                return;

            if (_client.GetChannel(_notificationChannelId.Value) is ISocketMessageChannel channel)
            {
                await channel.SendMessageAsync(embed: embed);
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError("恢复通知", $"向 Discord 发送恢复通知失败：{ex.Message}");
        }
    }

    /// <summary>
    /// Sends a custom recovery notification.
    /// </summary>
    public static async Task SendCustomNotificationAsync(string title, string description, Color color)
    {
        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(color)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter($"{_hubName} 恢复系统")
            .Build();

        await SendNotificationAsync(embed);
    }

    /// <summary>
    /// Sends a recovery summary report.
    /// </summary>
    public static async Task SendRecoverySummaryAsync<T>(BotRunner<T> runner, BotRecoveryService<T> recoveryService) 
        where T : class, IConsoleBotConfig
    {
        var embedBuilder = new EmbedBuilder()
            .WithTitle("📊 机器人恢复摘要")
            .WithColor(Color.Blue)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter($"{_hubName} 恢复系统");

        foreach (var bot in runner.Bots)
        {
            var state = bot.GetRecoveryState();
            if (state != null && (state.ConsecutiveFailures > 0 || state.CrashHistory.Count > 0))
            {
                var status = bot.IsRunning ? "🟢 正在运行" : "🔴 已停止";
                var fieldValue = $"状态：{status}\n" +
                                $"崩溃次数：{state.CrashHistory.Count}\n" +
                                $"连续失败：{state.ConsecutiveFailures}";
                
                embedBuilder.AddField(bot.Bot.Connection.Name, fieldValue, true);
            }
        }

        if (embedBuilder.Fields.Count == 0)
        {
            embedBuilder.WithDescription("所有机器人运行正常，近期没有崩溃记录。");
        }

        await SendNotificationAsync(embedBuilder.Build());
    }
}