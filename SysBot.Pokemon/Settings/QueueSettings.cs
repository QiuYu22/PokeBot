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

    [DisplayName("处理中允许退出")]
    [Category(FeatureToggle), Description("允许用户在交易过程中自行退出队列。")]
    public bool CanDequeueIfProcessing { get; set; }

    [DisplayName("允许加入队列")]
    [Category(FeatureToggle), Description("允许用户加入队列。")]
    public bool CanQueue { get; set; } = true;

    [DisplayName("预计等待倍数")]
    [Category(TimeBias), Description("估算用户等待时间时乘以队列人数的系数。")]
    public float EstimatedDelayFactor { get; set; } = 1.1f;

    [DisplayName("Flex 模式")]
    [Category(FeatureToggle), Description("指定 Flex 模式如何调度各类型队列。")]
    public FlexYieldMode FlexMode { get; set; } = FlexYieldMode.Weighted;

    [DisplayName("关闭持续时间（秒）")]
    [Category(QueueToggle), Description("定时模式：队列关闭后持续的秒数。")]
    public int IntervalCloseFor { get; set; } = 15 * 60;

    [DisplayName("开放持续时间（秒）")]
    [Category(QueueToggle), Description("定时模式：队列开放后持续的秒数。")]
    public int IntervalOpenFor { get; set; } = 5 * 60;

    // General
    [DisplayName("队列人数上限")]
    [Category(FeatureToggle), Description("当队列人数达到该值时，阻止新用户加入。")]
    public int MaxQueueCount { get; set; } = 30;

    [DisplayName("队列切换模式")]
    [Category(FeatureToggle), Description("设置队列开启与关闭的控制模式。")]
    public QueueOpening QueueToggleMode { get; set; } = QueueOpening.Threshold;

    [DisplayName("满员时发送通知")]
    [Category(FeatureToggle), Description("启用后，当队列因容量满额而关闭时向公告频道发送通知。")]
    public bool NotifyOnQueueClose { get; set; } = true;

    [DisplayName("关闭阈值人数")]
    [Category(QueueToggle), Description("阈值模式：达到该人数时关闭队列。")]
    public int ThresholdLock { get; set; } = 30;

    [DisplayName("开启阈值人数")]
    [Category(QueueToggle), Description("阈值模式：人数低于该值时重新开放队列。")]
    public int ThresholdUnlock { get; set; }

    [DisplayName("克隆队列人数权重")]
    [Category(UserBias), Description("按克隆队列中的用户数量调整权重。")]
    public int YieldMultCountClone { get; set; } = 100;

    [DisplayName("放生队列人数权重")]
    [Category(UserBias), Description("按放生队列中的用户数量调整权重。")]
    public int YieldMultCountDump { get; set; } = 100;

    [DisplayName("修复 OT 队列人数权重")]
    [Category(UserBias), Description("按修复 OT 队列中的用户数量调整权重。")]
    public int YieldMultCountFixOT { get; set; } = 100;

    [DisplayName("种子检测队列人数权重")]
    [Category(UserBias), Description("按种子检测队列中的用户数量调整权重。")]
    public int YieldMultCountSeedCheck { get; set; } = 100;

    [DisplayName("交易队列人数权重")]
    [Category(UserBias), Description("按交易队列中的用户数量调整权重。")]
    public int YieldMultCountTrade { get; set; } = 100;

    [DisplayName("时间权重模式")]
    [Category(TimeBias), Description("决定按时间累积的权重是与用户权重相加还是相乘。")]
    public FlexBiasMode YieldMultWait { get; set; } = FlexBiasMode.Multiply;

    [DisplayName("克隆等待时间权重")]
    [Category(TimeBias), Description("根据用户加入克隆队列后的等待时间增加权重。")]
    public int YieldMultWaitClone { get; set; } = 1;

    [DisplayName("放生等待时间权重")]
    [Category(TimeBias), Description("根据用户加入放生队列后的等待时间增加权重。")]
    public int YieldMultWaitDump { get; set; } = 1;

    [DisplayName("修复 OT 等待时间权重")]
    [Category(TimeBias), Description("根据用户加入修复 OT 队列后的等待时间增加权重。")]
    public int YieldMultWaitFixOT { get; set; } = 1;

    [DisplayName("种子检测等待时间权重")]
    [Category(TimeBias), Description("根据用户加入种子检测队列后的等待时间增加权重。")]
    public int YieldMultWaitSeedCheck { get; set; } = 1;

    // Queue Toggle
    // Flex Users
    // Flex Time
    [DisplayName("交易等待时间权重")]
    [Category(TimeBias), Description("根据用户加入交易队列后的等待时间增加权重。")]
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
