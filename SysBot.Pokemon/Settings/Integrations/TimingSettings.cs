using System.ComponentModel;

namespace SysBot.Pokemon;

public class TimingSettings
{
    private const string CloseGame = "关闭游戏";
    private const string Misc = "杂项";
    private const string OpenGame = "开启游戏";
    private const string Raid = "团战";

    [Category(Misc), DisplayName("避免系统更新"), Description("启用后自动拒绝系统更新提示。")]
    public bool AvoidSystemUpdate { get; set; } = true;

    [Category(Misc), DisplayName("重连等待时间(毫秒)"), Description("每次尝试重连之间额外等待的毫秒数（基础间隔为 30 秒）。")]
    public int ExtraReconnectDelay { get; set; }

    [Category(Raid), DisplayName("额外等待：接受好友"), Description("[RaidBot] 接受好友后额外等待的毫秒数。")]
    public int ExtraTimeAddFriend { get; set; }

    [Category(CloseGame), DisplayName("额外等待：关闭游戏"), Description("点选关闭游戏后额外等待的毫秒数。")]
    public int ExtraTimeCloseGame { get; set; }

    // Miscellaneous settings.
    [Category(Misc), DisplayName("额外等待：联网"), Description("[SWSH/SV/PLZA] 连接 Y-Comm（+）、联网（L）或 Portal 后额外等待的毫秒数；PLZA 默认基础等待为 8 秒。")]
    public int ExtraTimeConnectOnline { get; set; }

    [Category(Raid), DisplayName("额外等待：删除好友"), Description("[RaidBot] 删除好友后额外等待的毫秒数。")]
    public int ExtraTimeDeleteFriend { get; set; }

    [Category(Raid), DisplayName("额外等待：结束团战"), Description("[RaidBot] 重置团战前关闭游戏所需额外等待的毫秒数。")]
    public int ExtraTimeEndRaid { get; set; }

    [Category(Misc), DisplayName("额外等待：加入联盟房"), Description("[BDSP] 在调用交易前等待联盟房加载完成的额外毫秒数。")]
    public int ExtraTimeJoinUnionRoom { get; set; } = 500;

    [Category(Misc), DisplayName("额外等待：离开联盟房"), Description("[BDSP] 离开联盟房后等待场景加载完成的额外毫秒数。")]
    public int ExtraTimeLeaveUnionRoom { get; set; } = 1000;

    [Category(OpenGame), DisplayName("额外等待：标题画面"), Description("在标题画面按下 A 前额外等待的毫秒数。")]
    public int ExtraTimeLoadGame { get; set; } = 5000;

    [Category(OpenGame), DisplayName("额外等待：场景加载"), Description("从标题画面进入游戏后等待场景加载的额外毫秒数。")]
    public int ExtraTimeLoadOverworld { get; set; } = 3000;

    [Category(Misc), DisplayName("额外等待：宝可入口"), Description("[SV] 等待宝可入口加载完成的额外毫秒数。")]
    public int ExtraTimeLoadPortal { get; set; } = 1000;

    // Opening the game.
    [Category(OpenGame), DisplayName("需要选择用户档"), Description("启动游戏时若需要选择用户档，启用此项。")]
    public bool ProfileSelectionRequired { get; set; } = true;

    [Category(OpenGame), DisplayName("额外等待：载入用户档"), Description("启动游戏时等待用户档列表加载的额外毫秒数。")]
    public int ExtraTimeLoadProfile { get; set; }

    [Category(OpenGame), DisplayName("检测可游玩弹窗"), Description("启用后为“正在检测是否可游玩”弹窗增加等待。")]
    public bool CheckGameDelay { get; set; } = false;

    [Category(OpenGame), DisplayName("额外等待：可游玩弹窗"), Description("“正在检测是否可游玩”弹窗额外等待的毫秒数。")]
    public int ExtraTimeCheckGame { get; set; } = 200;

    // Raid-specific timings.
    [Category(Raid), DisplayName("额外等待：载入团战"), Description("[RaidBot] 点选巢穴后等待团战加载完成的额外毫秒数。")]
    public int ExtraTimeLoadRaid { get; set; }

    [Category(Misc), DisplayName("额外等待：载入盒子"), Description("找到交易后等待盒子界面加载完成的额外毫秒数。")]
    public int ExtraTimeOpenBox { get; set; } = 1000;

    [Category(Misc), DisplayName("额外等待：输入连线代码"), Description("交易过程中打开键盘输入连线代码后等待的毫秒数。")]
    public int ExtraTimeOpenCodeEntry { get; set; } = 1000;

    [Category(Raid), DisplayName("额外等待：邀请他人"), Description("[RaidBot] 点选“邀请他人”后，确认宝可梦前额外等待的毫秒数。")]
    public int ExtraTimeOpenRaid { get; set; }

    [Category(Misc), DisplayName("额外等待：Y 菜单"), Description("[BDSP] 每次交易循环开始时等待 Y 菜单加载的额外毫秒数。")]
    public int ExtraTimeOpenYMenu { get; set; } = 500;

    // Closing the game.
    [Category(CloseGame), DisplayName("额外等待：返回主界面"), Description("按下 HOME 最小化游戏后额外等待的毫秒数。")]
    public int ExtraTimeReturnHome { get; set; }

    [Category(Misc), DisplayName("按键等待时间"), Description("在导航 Switch 菜单或输入连接代码时，每次按键后的等待毫秒数。")]
    public int KeypressTime { get; set; } = 200;

    [Category(Misc), DisplayName("重连尝试次数"), Description("Socket 断开后尝试重新连接的次数，设为 -1 表示无限尝试。")]
    public int ReconnectAttempts { get; set; } = 30;
    public override string ToString() => "额外时间设置";
}
