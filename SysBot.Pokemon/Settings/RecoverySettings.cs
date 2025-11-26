using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Configuration settings for automatic bot recovery after crashes or cancellation token stops.
/// </summary>
public class RecoverySettings
{
    private const string Recovery = "恢复";

    [Category(Recovery), Description("启用对崩溃或停止的机器人的自动恢复尝试。"), DisplayName("启用恢复")]
    public bool EnableRecovery { get; set; } = true;

    [Category(Recovery), Description("放弃机器人之前的最大连续恢复尝试次数。"), DisplayName("最大恢复尝试次数")]
    public int MaxRecoveryAttempts { get; set; } = 3;

    [Category(Recovery), Description("尝试重启崩溃的机器人之前的初始延迟（秒）。"), DisplayName("初始恢复延迟")]
    public int InitialRecoveryDelaySeconds { get; set; } = 5;

    [Category(Recovery), Description("恢复尝试之间的最大延迟（秒）（用于指数退避）。"), DisplayName("最大恢复延迟")]
    public int MaxRecoveryDelaySeconds { get; set; } = 300; // 5 分钟

    [Category(Recovery), Description("指数退避的乘数（例如，2.0 每次将延迟翻倍）。"), DisplayName("退避乘数")]
    public double BackoffMultiplier { get; set; } = 2.0;

    [Category(Recovery), Description("跟踪崩溃历史的时间窗口（分钟）。此窗口之外的崩溃不计算在内。"), DisplayName("崩溃历史窗口")]
    public int CrashHistoryWindowMinutes { get; set; } = 60; // 1 小时

    [Category(Recovery), Description("永久关闭之前在历史窗口内允许的最大崩溃次数。"), DisplayName("窗口内最大崩溃数")]
    public int MaxCrashesInWindow { get; set; } = 5;

    [Category(Recovery), Description("为故意停止的机器人启用恢复（对网络断开有用）。"), DisplayName("恢复故意停止")]
    public bool RecoverIntentionalStops { get; set; } = false;

    [Category(Recovery), Description("成功恢复后重置尝试计数器前等待的延迟（秒）。"), DisplayName("成功恢复重置延迟")]
    public int SuccessfulRecoveryResetDelaySeconds { get; set; } = 300; // 5 分钟

    [Category(Recovery), Description("当机器人崩溃并尝试恢复时发送通知。"), DisplayName("恢复尝试时通知")]
    public bool NotifyOnRecoveryAttempt { get; set; } = true;

    [Category(Recovery), Description("当机器人在所有尝试后恢复失败时发送通知。"), DisplayName("恢复失败时通知")]
    public bool NotifyOnRecoveryFailure { get; set; } = true;

    [Category(Recovery), Description("机器人被视为稳定之前的最短运行时间（秒）（重置恢复尝试）。"), DisplayName("最短稳定运行时间")]
    public int MinimumStableUptimeSeconds { get; set; } = 600; // 10 分钟

    public override string ToString() => "机器人恢复设置";
}