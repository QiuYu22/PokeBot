using PKHeX.Core;
using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class RaidSettings : IBotStateSettings, ICountSettings
{
    private const string Counts = "统计";

    private const string FeatureToggle = "功能开关";

    private const string Hosting = "主机";

    private int _completedRaids;

    [Category(Counts), Description("已开始的团体战次数"), DisplayName("已完成团体战")]
    public int CompletedRaids
    {
        get => _completedRaids;
        set => _completedRaids = value;
    }

    [Category(FeatureToggle), Description("当每个队员锁定宝可梦时回显通知。"), DisplayName("回显队伍准备")]
    public bool EchoPartyReady { get; set; }

    [Category(Counts), Description("启用后，在请求状态检查时将输出统计数据。"), DisplayName("状态检查时输出统计")]
    public bool EmitCountsOnStatusCheck { get; set; }

    [Category(FeatureToggle), Description("允许机器人回显您的好友代码（如果设置）。"), DisplayName("好友代码")]
    public string FriendCode { get; set; } = string.Empty;

    [Category(Hosting), Description("在尝试添加/删除好友之前要主持的团体战次数。设置为 1 表示主持一次团体战后开始添加/删除好友。"), DisplayName("初始主持次数")]
    public int InitialRaidsToHost { get; set; }

    [Category(Hosting), Description("主持团体战的最大连接密码。设置为 -1 表示无密码主持。"), DisplayName("最大团体战密码")]
    public int MaxRaidCode { get; set; } = 8199;

    [Category(Hosting), Description("主持团体战的最小连接密码。设置为 -1 表示无密码主持。"), DisplayName("最小团体战密码")]
    public int MinRaidCode { get; set; } = 8180;

    [Category(Hosting), Description("每次接受的好友请求数量。"), DisplayName("添加好友数量")]
    public int NumberFriendsToAdd { get; set; }

    [Category(Hosting), Description("每次删除的好友数量。"), DisplayName("删除好友数量")]
    public int NumberFriendsToDelete { get; set; }

    [Category(Hosting), Description("您用于管理好友的 Nintendo Switch 配置文件编号。例如，如果使用第二个配置文件，请设置为 2。"), DisplayName("配置文件编号")]
    public int ProfileNumber { get; set; } = 1;

    [Category(FeatureToggle), Description("机器人主持的团体战的可选描述。留空则使用自动宝可梦检测。"), DisplayName("团体战描述")]
    public string RaidDescription { get; set; } = string.Empty;

    [Category(Hosting), Description("尝试添加好友之间要主持的团体战次数。"), DisplayName("添加好友间隔")]
    public int RaidsBetweenAddFriends { get; set; }

    [Category(Hosting), Description("尝试删除好友之间要主持的团体战次数。"), DisplayName("删除好友间隔")]
    public int RaidsBetweenDeleteFriends { get; set; }

    [Category(Hosting), Description("开始尝试添加好友的行号。"), DisplayName("添加好友起始行")]
    public int RowStartAddingFriends { get; set; } = 1;

    [Category(Hosting), Description("开始尝试删除好友的行号。"), DisplayName("删除好友起始行")]
    public int RowStartDeletingFriends { get; set; } = 1;

    [Category(FeatureToggle), Description("启用后，在正常机器人循环操作期间关闭屏幕以节省电量。"), DisplayName("关闭屏幕")]
    public bool ScreenOff { get; set; }

    [Category(Hosting), Description("尝试开始团体战前等待的秒数。范围从 0 到 180 秒。"), DisplayName("等待时间")]
    public int TimeToWait { get; set; } = 90;

    public int AddCompletedRaids() => Interlocked.Increment(ref _completedRaids);

    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedRaids != 0)
            yield return $"已开始团体战: {CompletedRaids}";
    }

    /// <summary>
    /// Gets a random trade code based on the range settings.
    /// </summary>
    public int GetRandomRaidCode() => Util.Rand.Next(MinRaidCode, MaxRaidCode + 1);

    public override string ToString() => "团体战机器人设置";
}
