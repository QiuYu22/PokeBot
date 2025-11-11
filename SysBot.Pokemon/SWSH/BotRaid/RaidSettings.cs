using PKHeX.Core;
using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class RaidSettings : IBotStateSettings, ICountSettings
{
private const string Counts = "计数";

private const string FeatureToggle = "功能开关";

private const string Hosting = "团战配置";

    private int _completedRaids;

    [Category(Counts), Description("已发起的团战次数")]
    public int CompletedRaids
    {
        get => _completedRaids;
        set => _completedRaids = value;
    }

    [Category(FeatureToggle), Description("当每位队员锁定宝可梦时播报对应信息。")]
    public bool EchoPartyReady { get; set; }

    [Category(Counts), Description("启用后，在请求状态检查时会输出计数信息。")]
    public bool EmitCountsOnStatusCheck { get; set; }

    [Category(FeatureToggle), Description("启用后若设置好友码，机器人会向外广播该好友码。")]
    public string FriendCode { get; set; } = string.Empty;

    [Category(Hosting), Description("计划在添加或删除好友之前先主持的团战次数。设为 1 表示完成一场团战后立即处理好友列表。")]
    public int InitialRaidsToHost { get; set; }

    [Category(Hosting), Description("主持团战时使用的联机密码上限，设为 -1 表示无需密码。")]
    public int MaxRaidCode { get; set; } = 8199;

    [Category(Hosting), Description("主持团战时使用的联机密码下限，设为 -1 表示无需密码。")]
    public int MinRaidCode { get; set; } = 8180;

    [Category(Hosting), Description("每次接受的好友申请数量。")]
    public int NumberFriendsToAdd { get; set; }

    [Category(Hosting), Description("每次删除的好友数量。")]
    public int NumberFriendsToDelete { get; set; }

    [Category(Hosting), Description("用于管理好友的 Nintendo Switch 账号序号，例如使用第二个账号时设为 2。")]
    public int ProfileNumber { get; set; } = 1;

    [Category(FeatureToggle), Description("可选的团战描述；留空时自动使用宝可梦识别结果。")]
    public string RaidDescription { get; set; } = string.Empty;

    [Category(Hosting), Description("每隔多少场团战尝试添加好友一次。")]
    public int RaidsBetweenAddFriends { get; set; }

    [Category(Hosting), Description("每隔多少场团战尝试删除好友一次。")]
    public int RaidsBetweenDeleteFriends { get; set; }

    [Category(Hosting), Description("尝试添加好友时从第几行开始。")]
    public int RowStartAddingFriends { get; set; } = 1;

    [Category(Hosting), Description("尝试删除好友时从第几行开始。")]
    public int RowStartDeletingFriends { get; set; } = 1;

    [Category(FeatureToggle), Description("启用后，在常规循环中会关闭屏幕以节省电量。")]
    public bool ScreenOff { get; set; }

    [Category(Hosting), Description("在尝试开始团战前等待的秒数，范围 0 到 180。")]
    public int TimeToWait { get; set; } = 90;

    public int AddCompletedRaids() => Interlocked.Increment(ref _completedRaids);

    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedRaids != 0)
            yield return $"已发起团战：{CompletedRaids}";
    }

    /// <summary>
    /// Gets a random trade code based on the range settings.
    /// </summary>
    public int GetRandomRaidCode() => Util.Rand.Next(MinRaidCode, MaxRaidCode + 1);

    public override string ToString() => "团战机器人设置";
}
