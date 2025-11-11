using PKHeX.Core;
using SysBot.Base;
using System.ComponentModel;

namespace SysBot.Pokemon;

public class DistributionSettings : ISynchronizationSetting
{
    private const string Distribute = "分发设置";

    private const string Synchronize = "同步设置";

    [DisplayName("闲置时分发")]
    [Category(Distribute), Description("启用后，空闲的联机交易机器人会随机分发分发目录中的 PKM 文件。")]
    public bool DistributeWhileIdle { get; set; } = true;

    [DisplayName("未匹配时退出 Ledy 交换")]
    [Category(Distribute), Description("启用后，当随机 Ledy 昵称交换未匹配成功时直接退出，而非随机发送候选宝可梦。")]
    public bool LedyQuitIfNoMatch { get; set; }

    [DisplayName("Ledy 匹配物种")]
    [Category(Distribute), Description("当设置为非 None 时，随机交易除昵称匹配外还需符合该指定物种。")]
    public Species LedySpecies { get; set; } = Species.None;

    [DisplayName("使用随机联机密码")]
    [Category(Distribute), Description("分发模式下的联机密码使用最小/最大范围而不是固定值。")]
    public bool RandomCode { get; set; }

    [DisplayName("BDSP 留在联盟房间")]
    [Category(Distribute), Description("针对 BDSP：分发机器人进入指定房间并保持停留，直到停止为止。")]
    public bool RemainInUnionRoomBDSP { get; set; } = true;

    // Distribute
    [DisplayName("随机分发顺序")]
    [Category(Distribute), Description("启用后，分发目录将随机输出文件，而非固定顺序。")]
    public bool Shuffled { get; set; }

    [DisplayName("机器人同步方式")]
    [Category(Synchronize), Description("联机交易：多台分发机器人同步确认交易密码。本地模式下所有机器人到达屏障后继续；远程模式下需外部信号触发。")]
    public BotSyncOption SynchronizeBots { get; set; } = BotSyncOption.LocalSync;

    // Synchronize
    [DisplayName("同步释放延迟（毫秒）")]
    [Category(Synchronize), Description("联机交易：全部机器人准备确认密码后，Hub 再等待指定毫秒数释放。")]
    public int SynchronizeDelayBarrier { get; set; }

    [DisplayName("同步等待超时（秒）")]
    [Category(Synchronize), Description("联机交易：机器人在放弃同步前允许等待的时间（秒）。")]
    public double SynchronizeTimeout { get; set; } = 90;

    [DisplayName("默认分发密码")]
    [Category(Distribute), Description("分发模式的默认联机交易密码。")]
    public int TradeCode { get; set; } = 7196;

    public override string ToString() => "分发交易设置";
}
