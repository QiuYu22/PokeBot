using System.ComponentModel;

namespace SysBot.Pokemon;

public class TimingSettings
{
    private const string CloseGame = "关闭游戏";

    private const string Misc = "杂项";

    private const string OpenGame = "打开游戏";

    private const string Raid = "团体战";

    [Category(Misc), Description("启用此选项以拒绝接受的系统更新。"), DisplayName("拒绝系统更新")]
    public bool AvoidSystemUpdate { get; set; } = true;

    [Category(Misc), Description("重新连接尝试之间等待的额外时间（毫秒）。基础时间为 30 秒。"), DisplayName("额外重连延迟")]
    public int ExtraReconnectDelay { get; set; }

    [Category(Raid), Description("[团体战机器人] 接受好友后等待的额外时间（毫秒）。"), DisplayName("添加好友额外时间")]
    public int ExtraTimeAddFriend { get; set; }

    [Category(CloseGame), Description("点击关闭游戏后等待的额外时间（毫秒）。"), DisplayName("关闭游戏额外时间")]
    public int ExtraTimeCloseGame { get; set; }

    // 杂项设置
    [Category(Misc), Description("[SWSH/SV/PLZA] 点击 + 连接 Y-Comm (SWSH)、L 连接在线 (SV) 或连接入口站 (PLZA) 后等待的额外时间（毫秒）。PLZA 的基础时间为 8 秒。"), DisplayName("连接在线额外时间")]
    public int ExtraTimeConnectOnline { get; set; }

    [Category(Raid), Description("[团体战机器人] 删除好友后等待的额外时间（毫秒）。"), DisplayName("删除好友额外时间")]
    public int ExtraTimeDeleteFriend { get; set; }

    [Category(Raid), Description("[团体战机器人] 关闭游戏以重置团体战前等待的额外时间（毫秒）。"), DisplayName("结束团体战额外时间")]
    public int ExtraTimeEndRaid { get; set; }

    [Category(Misc), Description("[BDSP] 在尝试呼叫交易前等待联盟房间加载的额外时间（毫秒）。"), DisplayName("加入联盟房间额外时间")]
    public int ExtraTimeJoinUnionRoom { get; set; } = 500;

    [Category(Misc), Description("[BDSP] 离开联盟房间后等待大地图加载的额外时间（毫秒）。"), DisplayName("离开联盟房间额外时间")]
    public int ExtraTimeLeaveUnionRoom { get; set; } = 1000;

    [Category(OpenGame), Description("在标题画面点击 A 前等待的额外时间（毫秒）。"), DisplayName("加载游戏额外时间")]
    public int ExtraTimeLoadGame { get; set; } = 5000;

    [Category(OpenGame), Description("标题画面后等待大地图加载的额外时间（毫秒）。"), DisplayName("加载大地图额外时间")]
    public int ExtraTimeLoadOverworld { get; set; } = 3000;

    [Category(Misc), Description("[SV] 等待宝可入口站加载的额外时间（毫秒）。"), DisplayName("加载入口站额外时间")]
    public int ExtraTimeLoadPortal { get; set; } = 1000;

    // 打开游戏
    [Category(OpenGame), Description("如果启动游戏时需要选择配置文件，请启用此选项。"), DisplayName("需要选择配置文件")]
    public bool ProfileSelectionRequired { get; set; } = true;

    [Category(OpenGame), Description("启动游戏时等待配置文件加载的额外时间（毫秒）。"), DisplayName("加载配置文件额外时间")]
    public int ExtraTimeLoadProfile { get; set; }

    [Category(OpenGame), Description("启用此选项以在\"正在检查是否可以游玩\"弹窗时添加延迟。"), DisplayName("检查游戏延迟")]
    public bool CheckGameDelay { get; set; } = false;

    [Category(OpenGame), Description("等待\"正在检查是否可以游玩\"弹窗的额外时间。"), DisplayName("检查游戏额外时间")]
    public int ExtraTimeCheckGame { get; set; } = 200;

    // 团体战特定时间
    [Category(Raid), Description("[团体战机器人] 点击巢穴后等待团体战加载的额外时间（毫秒）。"), DisplayName("加载团体战额外时间")]
    public int ExtraTimeLoadRaid { get; set; }

    [Category(Misc), Description("找到交易后等待盒子加载的额外时间（毫秒）。"), DisplayName("打开盒子额外时间")]
    public int ExtraTimeOpenBox { get; set; } = 1000;

    [Category(Misc), Description("交易期间打开键盘输入密码后等待的时间。"), DisplayName("打开密码输入额外时间")]
    public int ExtraTimeOpenCodeEntry { get; set; } = 1000;

    [Category(Raid), Description("[团体战机器人] 点击\"邀请他人\"后锁定宝可梦前等待的额外时间（毫秒）。"), DisplayName("打开团体战额外时间")]
    public int ExtraTimeOpenRaid { get; set; }

    [Category(Misc), Description("[BDSP] 每个交易循环开始时等待 Y 菜单加载的额外时间（毫秒）。"), DisplayName("打开 Y 菜单额外时间")]
    public int ExtraTimeOpenYMenu { get; set; } = 500;

    // 关闭游戏
    [Category(CloseGame), Description("按 HOME 最小化游戏后等待的额外时间（毫秒）。"), DisplayName("返回主页额外时间")]
    public int ExtraTimeReturnHome { get; set; }

    [Category(Misc), Description("在 Switch 菜单中导航或输入连接密码时每次按键后等待的时间。"), DisplayName("按键时间")]
    public int KeypressTime { get; set; } = 200;

    [Category(Misc), Description("连接丢失后尝试重新连接到套接字的次数。设置为 -1 表示无限尝试。"), DisplayName("重连尝试次数")]
    public int ReconnectAttempts { get; set; } = 30;
    public override string ToString() => "额外时间设置";
}
