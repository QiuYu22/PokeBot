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

    private const string Hosting = "团战主持";

    private int _completedRaids;

    [Category(Counts), DisplayName("已开启的团战数"), Description("已经主持（并统计）的团战次数。")]
    public int CompletedRaids
    {
        get => _completedRaids;
        set => _completedRaids = value;
    }

    [Category(FeatureToggle), DisplayName("回显队员准备情况"), Description("启用后，当每位队员锁定宝可梦时都会回显他们的选择。")]
    public bool EchoPartyReady { get; set; }

    [Category(Counts), DisplayName("状态查询时回显计数"), Description("启用后，当外部请求状态信息时，会回显当前统计数据。")]
    public bool EmitCountsOnStatusCheck { get; set; }

    [Category(FeatureToggle), DisplayName("好友代码"), Description("填写后，机器人会在提示中回显此好友代码。")]
    public string FriendCode { get; set; } = string.Empty;

    [Category(Hosting), DisplayName("初始主持次数"), Description("在尝试增删好友之前要主持的团战数量。设置为 1 表示主持 1 次后立即处理好友。")]
    public int InitialRaidsToHost { get; set; }

    [Category(Hosting), DisplayName("连接代码上限"), Description("用于主持团战的连接代码上限。设置为 -1 表示无代码。")]
    public int MaxRaidCode { get; set; } = 8199;

    [Category(Hosting), DisplayName("连接代码下限"), Description("用于主持团战的连接代码下限。设置为 -1 表示无代码。")]
    public int MinRaidCode { get; set; } = 8180;

    [Category(Hosting), DisplayName("每次添加好友数量"), Description("每轮要接受的好友请求数量。")]
    public int NumberFriendsToAdd { get; set; }

    [Category(Hosting), DisplayName("每次删除好友数量"), Description("每轮要清理的好友数量。")]
    public int NumberFriendsToDelete { get; set; }

    [Category(Hosting), DisplayName("好友管理使用的档位"), Description("用于管理好友的 Switch 账号档位。例如使用第二个档位则设置为 2。")]
    public int ProfileNumber { get; set; } = 1;

    [Category(FeatureToggle), DisplayName("团战描述"), Description("可选文本，留空时将自动检测宝可梦描述。")]
    public string RaidDescription { get; set; } = string.Empty;

    [Category(Hosting), DisplayName("添加好友间隔"), Description("每隔多少次团战后尝试添加好友。")]
    public int RaidsBetweenAddFriends { get; set; }

    [Category(Hosting), DisplayName("删除好友间隔"), Description("每隔多少次团战后尝试删除好友。")]
    public int RaidsBetweenDeleteFriends { get; set; }

    [Category(Hosting), DisplayName("添加好友起始行"), Description("在好友列表中从第几行开始尝试添加。")]
    public int RowStartAddingFriends { get; set; } = 1;

    [Category(Hosting), DisplayName("删除好友起始行"), Description("在好友列表中从第几行开始尝试删除。")]
    public int RowStartDeletingFriends { get; set; } = 1;

    [Category(FeatureToggle), DisplayName("循环期间关闭屏幕"), Description("启用后，在正常循环运行时会关闭屏幕以节省电力。")]
    public bool ScreenOff { get; set; }

    [Category(Hosting), DisplayName("开始团战前的等待秒数"), Description("在尝试开始团战前等待的秒数（0-180 秒）。")]
    public int TimeToWait { get; set; } = 90;

    public int AddCompletedRaids() => Interlocked.Increment(ref _completedRaids);

    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedRaids != 0)
            yield return $"已开启的团战数：{CompletedRaids}";
    }

    /// <summary>
    /// Gets a random trade code based on the range settings.
    /// </summary>
    public int GetRandomRaidCode() => Util.Rand.Next(MinRaidCode, MaxRaidCode + 1);

    public override string ToString() => "团战机器人设置";
}
