using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class TwitchSettings
{
    private const string Messages = "消息";

    private const string Operation = "运行";

    private const string Startup = "启动";

    [DisplayName("允许频道命令")]
    [Category(Operation), Description("启用后，机器人处理频道内发送的命令。")]
    public bool AllowCommandsViaChannel { get; set; } = true;

    [DisplayName("允许密语命令")]
    [Category(Operation), Description("启用后允许用户通过密语发送命令（可绕过慢速模式）。")]
    public bool AllowCommandsViaWhisper { get; set; }

    [DisplayName("消息频道")]
    [Category(Startup), Description("发送消息到的频道名称。")]
    public string Channel { get; set; } = string.Empty;

    [DisplayName("命令前缀")]
    [Category(Startup), Description("机器人命令前缀。")]
    public char CommandPrefix { get; set; } = '$';

    [DisplayName("Discord 链接")]
    [Category(Operation), Description("Discord 服务器链接。")]
    public string DiscordLink { get; set; } = string.Empty;

    [DisplayName("分发倒计时")]
    [Category(Messages), Description("控制分发交易开始前是否进行倒计时。")]
    public bool DistributionCountDown { get; set; } = true;

    [DisplayName("赞助链接")]
    [Category(Operation), Description("赞助 / 打赏链接。")]
    public string DonationLink { get; set; } = string.Empty;

    [DisplayName("开始交易消息")]
    [Category(Operation), Description("解除屏障（开始交易）时发送的消息。")]
    public string MessageStart { get; set; } = string.Empty;

    [DisplayName("普通通知目标")]
    [Category(Messages), Description("通用通知发送的位置。")]
    public TwitchMessageDestination NotifyDestination { get; set; }

    [DisplayName("Sudo 用户列表")]
    [Category(Operation), Description("拥有 Sudo 权限的用户名。")]
    public string SudoList { get; set; } = string.Empty;

    [DisplayName("消息限流条数")]
    [Category(Operation), Description("在过去 Y 秒内发送消息超过 X 条则限流。")]
    public int ThrottleMessages { get; set; } = 100;

    // Messaging
    [DisplayName("消息限流窗口（秒）")]
    [Category(Operation), Description("在过去 Y 秒内发送消息超过 X 条则限流（秒数）。")]
    public double ThrottleSeconds { get; set; } = 30;

    [DisplayName("密语限流条数")]
    [Category(Operation), Description("在过去 Y 秒内发送密语超过 X 条则限流。")]
    public int ThrottleWhispers { get; set; } = 100;

    [DisplayName("密语限流窗口（秒）")]
    [Category(Operation), Description("在过去 Y 秒内发送密语超过 X 条则限流（秒数）。")]
    public double ThrottleWhispersSeconds { get; set; } = 60;

    [DisplayName("登录 Token")]
    [Category(Startup), Description("机器人登录 Token。")]
    public string Token { get; set; } = string.Empty;

    [DisplayName("交易取消通知目标")]
    [Category(Messages), Description("交易取消通知的发送位置。")]
    public TwitchMessageDestination TradeCanceledDestination { get; set; } = TwitchMessageDestination.Channel;

    [DisplayName("交易完成通知目标")]
    [Category(Messages), Description("交易完成通知的发送位置。")]
    public TwitchMessageDestination TradeFinishDestination { get; set; }

    [DisplayName("交易搜索通知目标")]
    [Category(Messages), Description("交易搜索通知的发送位置。")]
    public TwitchMessageDestination TradeSearchDestination { get; set; }

    // Message Destinations
    [DisplayName("交易开始通知目标")]
    [Category(Messages), Description("交易开始通知的发送位置。")]
    public TwitchMessageDestination TradeStartDestination { get; set; } = TwitchMessageDestination.Channel;

    [DisplayName("使用教程链接")]
    [Category(Operation), Description("机器人使用教程链接。")]
    public string TutorialLink { get; set; } = string.Empty;

    [DisplayName("使用教程文本")]
    [Category(Operation), Description("机器人使用教程的文本内容。")]
    public string TutorialText { get; set; } = string.Empty;

    // Operation
    [DisplayName("黑名单用户")]
    [Category(Operation), Description("禁止使用机器人的用户名列表。")]
    public string UserBlacklist { get; set; } = string.Empty;

    // Startup
    [DisplayName("机器人用户名")]
    [Category(Startup), Description("机器人登录的用户名。")]
    public string Username { get; set; } = string.Empty;

    public bool IsSudo(string username)
    {
        var sudos = SudoList.Split([",", ", ", " "], StringSplitOptions.RemoveEmptyEntries);
        return sudos.Contains(username);
    }

    public override string ToString() => "Twitch 集成设置";
}

public enum TwitchMessageDestination
{
    Disabled,

    Channel,

    Whisper,
}
