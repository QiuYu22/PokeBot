using System;
using System.ComponentModel;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon;

public enum FlexBiasMode
{
    Add,

    Multiply,
}

public enum FlexYieldMode
{
    LessCheatyFirst,

    Weighted,
}

public class QueueSettings
{
    private const string FeatureToggle = "功能开关";

    private const string QueueToggle = "队列开关";

    private const string TimeBias = "时间权重";

    private const string UserBias = "用户权重";

    [Category(FeatureToggle), Description("允许用户在交易进行中退出队列。"), DisplayName("处理中可退出")]
    public bool CanDequeueIfProcessing { get; set; }

    [Category(FeatureToggle), Description("切换用户是否可以加入队列。"), DisplayName("可以加入队列")]
    public bool CanQueue { get; set; } = true;

    [Category(TimeBias), Description("乘以队列中的用户数量，以估算用户被处理前需要等待的时间。"), DisplayName("预计延迟因子")]
    public float EstimatedDelayFactor { get; set; } = 1.1f;

    [Category(FeatureToggle), Description("确定弹性模式将如何处理队列。"), DisplayName("弹性模式")]
    public FlexYieldMode FlexMode { get; set; } = FlexYieldMode.Weighted;

    [Category(QueueToggle), Description("定时模式：队列解锁前关闭的秒数。"), DisplayName("关闭间隔")]
    public int IntervalCloseFor { get; set; } = 15 * 60;

    [Category(QueueToggle), Description("定时模式：队列锁定前开放的秒数。"), DisplayName("开放间隔")]
    public int IntervalOpenFor { get; set; } = 5 * 60;

    // 常规
    [Category(FeatureToggle), Description("如果队列中已有这么多用户，则阻止添加新用户。"), DisplayName("最大队列数")]
    public int MaxQueueCount { get; set; } = 30;

    [Category(FeatureToggle), Description("确定队列何时打开和关闭。"), DisplayName("队列开关模式")]
    public QueueOpening QueueToggleMode { get; set; } = QueueOpening.Threshold;

    [Category(FeatureToggle), Description("启用后，当队列因达到最大容量而关闭时，会向公告频道发送嵌入通知。"), DisplayName("队列关闭时通知")]
    public bool NotifyOnQueueClose { get; set; } = true;

    [Category(QueueToggle), Description("阈值模式：导致队列关闭的用户数量。"), DisplayName("锁定阈值")]
    public int ThresholdLock { get; set; } = 30;

    [Category(QueueToggle), Description("阈值模式：导致队列开放的用户数量。"), DisplayName("解锁阈值")]
    public int ThresholdUnlock { get; set; }

    [Category(UserBias), Description("根据队列中的用户数量调整克隆队列的权重。"), DisplayName("克隆队列权重")]
    public int YieldMultCountClone { get; set; } = 100;

    [Category(UserBias), Description("根据队列中的用户数量调整转储队列的权重。"), DisplayName("转储队列权重")]
    public int YieldMultCountDump { get; set; } = 100;

    [Category(UserBias), Description("根据队列中的用户数量调整 FixOT 队列的权重。"), DisplayName("FixOT 队列权重")]
    public int YieldMultCountFixOT { get; set; } = 100;

    [Category(UserBias), Description("根据队列中的用户数量调整种子检查队列的权重。"), DisplayName("种子检查队列权重")]
    public int YieldMultCountSeedCheck { get; set; } = 100;

    [Category(UserBias), Description("根据队列中的用户数量调整交易队列的权重。"), DisplayName("交易队列权重")]
    public int YieldMultCountTrade { get; set; } = 100;

    [Category(TimeBias), Description("确定权重应该加到总权重还是乘以总权重。"), DisplayName("等待权重模式")]
    public FlexBiasMode YieldMultWait { get; set; } = FlexBiasMode.Multiply;

    [Category(TimeBias), Description("检查用户加入克隆队列后经过的时间，并相应增加队列的权重。"), DisplayName("克隆等待权重")]
    public int YieldMultWaitClone { get; set; } = 1;

    [Category(TimeBias), Description("检查用户加入转储队列后经过的时间，并相应增加队列的权重。"), DisplayName("转储等待权重")]
    public int YieldMultWaitDump { get; set; } = 1;

    [Category(TimeBias), Description("检查用户加入 FixOT 队列后经过的时间，并相应增加队列的权重。"), DisplayName("FixOT 等待权重")]
    public int YieldMultWaitFixOT { get; set; } = 1;

    [Category(TimeBias), Description("检查用户加入种子检查队列后经过的时间，并相应增加队列的权重。"), DisplayName("种子检查等待权重")]
    public int YieldMultWaitSeedCheck { get; set; } = 1;

    // 队列开关
    // 弹性用户
    // 弹性时间
    [Category(TimeBias), Description("检查用户加入交易队列后经过的时间，并相应增加队列的权重。"), DisplayName("交易等待权重")]
    public int YieldMultWaitTrade { get; set; } = 1;

    /// <summary>
    /// Estimates the amount of time (minutes) until the user will be processed.
    /// </summary>
    /// <param name="position">Position in the queue</param>
    /// <param name="botct">Amount of bots processing requests</param>
    /// <returns>Estimated time in Minutes</returns>
    public float EstimateDelay(int position, int botct) => (EstimatedDelayFactor * position) / botct;

    /// <summary>
    /// Gets the weight of a <see cref="PokeTradeType"/> based on the count of users in the queue and time users have waited.
    /// </summary>
    /// <param name="count">Count of users for <see cref="type"/></param>
    /// <param name="time">Next-to-be-processed user's time joining the queue</param>
    /// <param name="type">Queue type</param>
    /// <returns>Effective weight for the trade type.</returns>
    public long GetWeight(int count, DateTime time, PokeTradeType type)
    {
        var now = DateTime.Now;
        var seconds = (now - time).Seconds;

        var cb = GetCountBias(type) * count;
        var tb = GetTimeBias(type) * seconds;

        return YieldMultWait switch
        {
            FlexBiasMode.Multiply => cb * tb,
            _ => cb + tb,
        };
    }

    public override string ToString() => "队列加入设置";

    private int GetCountBias(PokeTradeType type) => type switch
    {
        PokeTradeType.Seed => YieldMultCountSeedCheck,
        PokeTradeType.Clone => YieldMultCountClone,
        PokeTradeType.Dump => YieldMultCountDump,
        PokeTradeType.FixOT => YieldMultCountFixOT,
        _ => YieldMultCountTrade,
    };

    private int GetTimeBias(PokeTradeType type) => type switch
    {
        PokeTradeType.Seed => YieldMultWaitSeedCheck,
        PokeTradeType.Clone => YieldMultWaitClone,
        PokeTradeType.Dump => YieldMultWaitDump,
        PokeTradeType.FixOT => YieldMultWaitFixOT,
        _ => YieldMultWaitTrade,
    };
}

public enum QueueOpening
{
    Manual,

    Threshold,

    Interval,
}
