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

    private const string QueueToggle = "队列切换";

    private const string TimeBias = "时间权重";

    private const string UserBias = "用户权重";

    [Category(FeatureToggle), DisplayName("交易中允许出队"), Description("启用后，正在交易中的用户仍可被移出队列。")]
    public bool CanDequeueIfProcessing { get; set; }

    [Category(FeatureToggle), DisplayName("允许加入队列"), Description("控制是否允许用户加入队列。")]
    public bool CanQueue { get; set; } = true;

    [Category(TimeBias), DisplayName("预计延迟系数"), Description("根据队列人数估算处理时间的乘数因子。")]
    public float EstimatedDelayFactor { get; set; } = 1.1f;

    [Category(FeatureToggle), DisplayName("Flex 模式"), Description("决定 Flex 模式如何处理队列。")]
    public FlexYieldMode FlexMode { get; set; } = FlexYieldMode.Weighted;

    [Category(QueueToggle), DisplayName("定时关闭时长 (秒)"), Description("定时模式下，队列关闭后保持的秒数。")]
    public int IntervalCloseFor { get; set; } = 15 * 60;

    [Category(QueueToggle), DisplayName("定时打开时长 (秒)"), Description("定时模式下，队列打开后持续的秒数。")]
    public int IntervalOpenFor { get; set; } = 5 * 60;

    // General
    [Category(FeatureToggle), DisplayName("最大排队人数"), Description("当队列人数达到该值时，阻止新的用户加入。")]
    public int MaxQueueCount { get; set; } = 30;

    [Category(FeatureToggle), DisplayName("队列开关模式"), Description("决定队列何时开启或关闭。")]
    public QueueOpening QueueToggleMode { get; set; } = QueueOpening.Threshold;

    [Category(FeatureToggle), DisplayName("队列关闭通知"), Description("启用后，当队列因满员关闭时会向公告频道发送嵌入通知。")]
    public bool NotifyOnQueueClose { get; set; } = true;

    [Category(QueueToggle), DisplayName("阈值模式关闭人数"), Description("阈值模式下触发队列关闭的用户数量。")]
    public int ThresholdLock { get; set; } = 30;

    [Category(QueueToggle), DisplayName("阈值模式开放人数"), Description("阈值模式下触发队列重新开放的用户数量。")]
    public int ThresholdUnlock { get; set; }

    [Category(UserBias), DisplayName("克隆队列人数权重"), Description("根据克隆队列人数调整其权重。")]
    public int YieldMultCountClone { get; set; } = 100;

    [Category(UserBias), DisplayName("转储队列人数权重"), Description("根据转储队列人数调整其权重。")]
    public int YieldMultCountDump { get; set; } = 100;

    [Category(UserBias), DisplayName("修复OT 队列人数权重"), Description("根据 FixOT 队列人数调整其权重。")]
    public int YieldMultCountFixOT { get; set; } = 100;

    [Category(UserBias), DisplayName("种子检查队列人数权重"), Description("根据种子检查队列人数调整其权重。")]
    public int YieldMultCountSeedCheck { get; set; } = 100;

    [Category(UserBias), DisplayName("普通交易队列人数权重"), Description("根据普通交易队列人数调整其权重。")]
    public int YieldMultCountTrade { get; set; } = 100;

    [Category(TimeBias), DisplayName("时间权重叠加方式"), Description("决定时间权重是与计数权重相乘还是相加。")]
    public FlexBiasMode YieldMultWait { get; set; } = FlexBiasMode.Multiply;

    [Category(TimeBias), DisplayName("克隆队列时间权重"), Description("根据加入克隆队列后的等待时间增加权重。")]
    public int YieldMultWaitClone { get; set; } = 1;

    [Category(TimeBias), DisplayName("转储队列时间权重"), Description("根据加入转储队列后的等待时间增加权重。")]
    public int YieldMultWaitDump { get; set; } = 1;

    [Category(TimeBias), DisplayName("修复OT 队列时间权重"), Description("根据加入 FixOT 队列后的等待时间增加权重。")]
    public int YieldMultWaitFixOT { get; set; } = 1;

    [Category(TimeBias), DisplayName("种子检查时间权重"), Description("根据加入种子检查队列后的等待时间增加权重。")]
    public int YieldMultWaitSeedCheck { get; set; } = 1;

    // Queue Toggle
    // Flex Users
    // Flex Time
    [Category(TimeBias), DisplayName("交易队列时间权重"), Description("根据加入普通交易队列后的等待时间增加权重。")]
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
