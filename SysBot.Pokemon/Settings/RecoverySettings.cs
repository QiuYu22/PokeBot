using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Configuration settings for automatic bot recovery after crashes or cancellation token stops.
/// </summary>
public class RecoverySettings
{
    private const string Recovery = "自动恢复";

    [DisplayName("启用自动恢复")]
    [Category(Recovery), Description("启用后，机器人在崩溃或被停止时会自动尝试恢复。")]
    public bool EnableRecovery { get; set; } = true;

    [DisplayName("最大连续恢复次数")]
    [Category(Recovery), Description("单个机器人连续恢复失败的最大尝试次数，超过后停止恢复。")]
    public int MaxRecoveryAttempts { get; set; } = 3;

    [DisplayName("首次恢复延迟（秒）")]
    [Category(Recovery), Description("首次尝试重新启动崩溃机器人的延迟（秒）。")]
    public int InitialRecoveryDelaySeconds { get; set; } = 5;

    [DisplayName("最大恢复延迟（秒）")]
    [Category(Recovery), Description("指数退避机制中的最大恢复间隔（秒）。")]
    public int MaxRecoveryDelaySeconds { get; set; } = 300; // 5 minutes

    [DisplayName("延迟倍增系数")]
    [Category(Recovery), Description("指数退避的倍增系数，例如 2.0 代表每次延迟翻倍。")]
    public double BackoffMultiplier { get; set; } = 2.0;

    [DisplayName("崩溃统计窗口（分钟）")]
    [Category(Recovery), Description("统计崩溃历史的时间窗口（分钟），超出窗口的崩溃不计入统计。")]
    public int CrashHistoryWindowMinutes { get; set; } = 60; // 1 hour

    [DisplayName("窗口内崩溃上限")]
    [Category(Recovery), Description("在统计窗口内允许的最多崩溃次数，超过后将永久停用该机器人。")]
    public int MaxCrashesInWindow { get; set; } = 5;

    [DisplayName("恢复手动停止的机器人")]
    [Category(Recovery), Description("对被人为停止的机器人也执行恢复（适用于网络掉线等情况）。")]
    public bool RecoverIntentionalStops { get; set; } = false;

    [DisplayName("成功后重置延迟（秒）")]
    [Category(Recovery), Description("成功恢复后等待多少秒再重置尝试计数。")]
    public int SuccessfulRecoveryResetDelaySeconds { get; set; } = 300; // 5 minutes

    [DisplayName("恢复尝试通知")]
    [Category(Recovery), Description("机器人崩溃并尝试恢复时发送通知。")]
    public bool NotifyOnRecoveryAttempt { get; set; } = true;

    [DisplayName("恢复失败通知")]
    [Category(Recovery), Description("在所有恢复尝试失败后发送通知。")]
    public bool NotifyOnRecoveryFailure { get; set; } = true;

    [DisplayName("稳定运行判定时间（秒）")]
    [Category(Recovery), Description("机器人连续运行达到该秒数后视为稳定，并重置恢复尝试计数。")]
    public int MinimumStableUptimeSeconds { get; set; } = 600; // 10 minutes

    public override string ToString() => "机器人自动恢复设置";
}