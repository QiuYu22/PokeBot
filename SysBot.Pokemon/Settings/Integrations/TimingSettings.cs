using System.ComponentModel;

namespace SysBot.Pokemon;

public class TimingSettings
{
    private const string CloseGame = "关闭游戏";

    private const string Misc = "通用";

    private const string OpenGame = "开启游戏";

    private const string Raid = "极巨团体战";

    [DisplayName("拒绝系统更新")]
    [Category(Misc), Description("启用后自动拒绝系统更新提示。")]
    public bool AvoidSystemUpdate { get; set; }

    [DisplayName("额外重连延迟")]
    [Category(Misc), Description("重连尝试之间额外等待的毫秒数，基础时间为 30 秒。")]
    public int ExtraReconnectDelay { get; set; }

    [DisplayName("团战加好友延迟")]
    [Category(Raid), Description("[团战机器人] 接受好友后额外等待的毫秒数。")]
    public int ExtraTimeAddFriend { get; set; }

    [DisplayName("关闭游戏后延迟")]
    [Category(CloseGame), Description("点击关闭游戏后额外等待的毫秒数。")]
    public int ExtraTimeCloseGame { get; set; }

    // Miscellaneous settings.
    [DisplayName("额外联机延迟")]
    [Category(Misc), Description("[SWSH/SV/PLZA] 连接 Y-Comm / 联机 / 传送门后额外等待的毫秒数，PLZA 基础时间为 8 秒。")]
    public int ExtraTimeConnectOnline { get; set; }

    [DisplayName("团战删好友延迟")]
    [Category(Raid), Description("[团战机器人] 删除好友后额外等待的毫秒数。")]
    public int ExtraTimeDeleteFriend { get; set; }

    [DisplayName("团战结束延迟")]
    [Category(Raid), Description("[团战机器人] 重置团战前关闭游戏所需的额外等待毫秒数。")]
    public int ExtraTimeEndRaid { get; set; }

    [DisplayName("进入联盟房间延迟")]
    [Category(Misc), Description("[BDSP] 进入联盟房间后等待其加载完成的额外毫秒数。")]
    public int ExtraTimeJoinUnionRoom { get; set; } = 500;

    [DisplayName("离开联盟房间延迟")]
    [Category(Misc), Description("[BDSP] 离开联盟房间后等待场景加载的额外毫秒数。")]
    public int ExtraTimeLeaveUnionRoom { get; set; } = 1000;

    [DisplayName("标题界面确认延迟")]
    [Category(OpenGame), Description("标题界面按下 A 前额外等待的毫秒数。")]
    public int ExtraTimeLoadGame { get; set; } = 5000;

    [DisplayName("场景加载延迟")]
    [Category(OpenGame), Description("标题界面后等待场景加载的额外毫秒数。")]
    public int ExtraTimeLoadOverworld { get; set; } = 3000;

    [DisplayName("入口加载延迟")]
    [Category(Misc), Description("[SV] 宝可入口加载的额外等待毫秒数。")]
    public int ExtraTimeLoadPortal { get; set; } = 1000;

    // Opening the game.
    [DisplayName("需要选择档案")]
    [Category(OpenGame), Description("启动游戏需要选择用户档案时启用。")]
    public bool ProfileSelectionRequired { get; set; } = true;

    [DisplayName("档案加载延迟")]
    [Category(OpenGame), Description("启动游戏时等待用户档案加载的额外毫秒数。")]
    public int ExtraTimeLoadProfile { get; set; }

    [DisplayName("检查可游玩弹窗处理")]
    [Category(OpenGame), Description("启用后，为“正在检查是否可游玩”弹窗增加等待。")]
    public bool CheckGameDelay { get; set; } = false;

    [DisplayName("检查弹窗延迟")]
    [Category(OpenGame), Description("“正在检查是否可游玩”弹窗的额外等待毫秒数。")]
    public int ExtraTimeCheckGame { get; set; } = 200;

    // Raid-specific timings.
    [DisplayName("团战加载延迟")]
    [Category(Raid), Description("[团战机器人] 点击巢穴后等待团战加载的额外毫秒数。")]
    public int ExtraTimeLoadRaid { get; set; }

    [DisplayName("宝可梦箱加载延迟")]
    [Category(Misc), Description("匹配到交易后等待宝可梦箱子加载的额外毫秒数。")]
    public int ExtraTimeOpenBox { get; set; } = 1000;

    [DisplayName("打开密码键盘延迟")]
    [Category(Misc), Description("交易时打开密码键盘后的等待毫秒数。")]
    public int ExtraTimeOpenCodeEntry { get; set; } = 1000;

    [DisplayName("邀请他人延迟")]
    [Category(Raid), Description("[团战机器人] 选择“邀请他人”后，在锁定宝可梦前额外等待的毫秒数。")]
    public int ExtraTimeOpenRaid { get; set; }

    [DisplayName("Y 菜单加载延迟")]
    [Category(Misc), Description("[BDSP] 每次交易循环开始时等待 Y 菜单加载的额外毫秒数。")]
    public int ExtraTimeOpenYMenu { get; set; } = 500;

    // Closing the game.
    [DisplayName("返回主界面延迟")]
    [Category(CloseGame), Description("按下 HOME 回到主界面后额外等待的毫秒数。")]
    public int ExtraTimeReturnHome { get; set; }

    [DisplayName("按键延迟")]
    [Category(Misc), Description("在导航 Switch 菜单或输入联机密码时每次按键后的等待毫秒数。")]
    public int KeypressTime { get; set; } = 200;

    [DisplayName("重连尝试次数")]
    [Category(Misc), Description("连接丢失后尝试重新连接的次数，设为 -1 表示无限重试。")]
    public int ReconnectAttempts { get; set; } = 30;
    public override string ToString() => "时间延迟设置";
}
