using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Configuration settings for automatic bot recovery after crashes or cancellation token stops.
/// </summary>
public class RecoverySettings
{
    private const string Recovery = "恢复";

    [Category(Recovery), DisplayName("启用自动恢复"), Description("启用后，当机器人崩溃或停止时自动尝试恢复。")]
    public bool EnableRecovery { get; set; } = true;

    [Category(Recovery), DisplayName("最大恢复尝试次数"), Description("放弃恢复前允许的连续尝试次数。")]
    public int MaxRecoveryAttempts { get; set; } = 3;

    [Category(Recovery), DisplayName("初始恢复延迟 (秒)"), Description("首次重启崩溃机器人前等待的秒数。")]
    public int InitialRecoveryDelaySeconds { get; set; } = 5;

    [Category(Recovery), DisplayName("最大恢复延迟 (秒)"), Description("指数退避时恢复尝试之间的最大等待秒数。")]
    public int MaxRecoveryDelaySeconds { get; set; } = 300; // 5 minutes

    [Category(Recovery), DisplayName("退避倍率"), Description("指数退避的倍率，例如 2.0 表示每次延迟翻倍。")]
    public double BackoffMultiplier { get; set; } = 2.0;

    [Category(Recovery), DisplayName("崩溃历史窗口 (分钟)"), Description("统计崩溃历史的时间窗口，超过该窗口的崩溃不计入。")]
    public int CrashHistoryWindowMinutes { get; set; } = 60; // 1 hour

    [Category(Recovery), DisplayName("窗口内最大崩溃数"), Description("在历史窗口内允许的最大崩溃次数，超过后永久停机。")]
    public int MaxCrashesInWindow { get; set; } = 5;

    [Category(Recovery), DisplayName("恢复主动停止"), Description("允许对因主动停止（如网络断线）而关闭的机器人执行恢复。")]
    public bool RecoverIntentionalStops { get; set; } = false;

    [Category(Recovery), DisplayName("恢复成功后重置延迟 (秒)"), Description("恢复成功后等待多少秒再重置尝试计数。")]
    public int SuccessfulRecoveryResetDelaySeconds { get; set; } = 300; // 5 minutes

    [Category(Recovery), DisplayName("恢复尝试通知"), Description("机器人崩溃并开始恢复时发送通知。")]
    public bool NotifyOnRecoveryAttempt { get; set; } = true;

    [Category(Recovery), DisplayName("恢复失败通知"), Description("机器人在耗尽所有尝试后仍无法恢复时发送通知。")]
    public bool NotifyOnRecoveryFailure { get; set; } = true;

    [Category(Recovery), DisplayName("稳定运行最短时间 (秒)"), Description("机器人维持稳定运行并重置恢复计数所需的最短秒数。")]
    public int MinimumStableUptimeSeconds { get; set; } = 600; // 10 minutes

    public override string ToString() => "机器人恢复设置";
}