using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class TwitchSettings
{
    private const string Messages = "消息";
    private const string Operation = "运行";
    private const string Startup = "启动";

    [Category(Operation), DisplayName("允许频道指令"), Description("启用后，机器人会响应频道内的指令。")]
    public bool AllowCommandsViaChannel { get; set; } = true;

    [Category(Operation), DisplayName("允许私信指令"), Description("启用后，用户可通过私信发送指令（无视慢速模式）。")]
    public bool AllowCommandsViaWhisper { get; set; }

    [Category(Startup), DisplayName("消息发送频道"), Description("机器人默认发送消息的频道。")]
    public string Channel { get; set; } = string.Empty;

    [Category(Startup), DisplayName("指令前缀"), Description("机器人接收指令时使用的前缀。")]
    public char CommandPrefix { get; set; } = '$';

    [Category(Operation), DisplayName("Discord 链接"), Description("Discord 服务器链接。")]
    public string DiscordLink { get; set; } = string.Empty;

    [Category(Messages), DisplayName("派发倒计时"), Description("开启后派发交易会在开始前倒计时。")]
    public bool DistributionCountDown { get; set; } = true;

    [Category(Operation), DisplayName("捐赠链接"), Description("展示给观众的捐赠链接。")]
    public string DonationLink { get; set; } = string.Empty;

    [Category(Operation), DisplayName("屏障释放消息"), Description("屏障放行时发送的提示消息。")]
    public string MessageStart { get; set; } = string.Empty;

    [Category(Messages), DisplayName("通知发送位置"), Description("通用通知发送到哪个目的地。")]
    public TwitchMessageDestination NotifyDestination { get; set; }

    [Category(Operation), DisplayName("Sudo 用户列表"), Description("拥有 Sudo 权限的用户名列表。")]
    public string SudoList { get; set; } = string.Empty;

    [Category(Operation), DisplayName("消息限流数量"), Description("在最近 Y 秒内若发送了 X 条消息，阻止机器人继续发言。")]
    public int ThrottleMessages { get; set; } = 100;

    // Messaging
    [Category(Operation), DisplayName("消息限流时间"), Description("与消息限流数量配合使用的时间窗口（秒）。")]
    public double ThrottleSeconds { get; set; } = 30;

    [Category(Operation), DisplayName("私信限流数量"), Description("在最近 Y 秒内若发送了 X 条私信，阻止机器人继续发送。")]
    public int ThrottleWhispers { get; set; } = 100;

    [Category(Operation), DisplayName("私信限流时间"), Description("与私信限流数量配合使用的时间窗口（秒）。")]
    public double ThrottleWhispersSeconds { get; set; } = 60;

    [Category(Startup), DisplayName("机器人登录 Token"), Description("Twitch 机器人登录所需的 Token。")]
    public string Token { get; set; } = string.Empty;

    [Category(Messages), DisplayName("交易取消通知"), Description("交易取消通知发送到哪个目的地。")]
    public TwitchMessageDestination TradeCanceledDestination { get; set; } = TwitchMessageDestination.Channel;

    [Category(Messages), DisplayName("交易完成通知"), Description("交易完成通知发送到哪个目的地。")]
    public TwitchMessageDestination TradeFinishDestination { get; set; }

    [Category(Messages), DisplayName("交易搜索通知"), Description("搜索到交易时通知发送到哪个目的地。")]
    public TwitchMessageDestination TradeSearchDestination { get; set; }

    // Message Destinations
    [Category(Messages), DisplayName("交易开始通知"), Description("交易开始通知发送到哪个目的地。")]
    public TwitchMessageDestination TradeStartDestination { get; set; } = TwitchMessageDestination.Channel;

    [Category(Operation), DisplayName("教程链接"), Description("机器人使用教程的链接。")]
    public string TutorialLink { get; set; } = string.Empty;

    [Category(Operation), DisplayName("教程文本"), Description("机器人使用教程的文本内容。")]
    public string TutorialText { get; set; } = string.Empty;

    // Operation
    [Category(Operation), DisplayName("用户黑名单"), Description("这些用户名的用户不能使用机器人。")]
    public string UserBlacklist { get; set; } = string.Empty;

    // Startup
    [Category(Startup), DisplayName("机器人用户名"), Description("用于登录的 Twitch 用户名。")]
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
