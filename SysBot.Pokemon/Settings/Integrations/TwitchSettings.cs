using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class TwitchSettings
{
    private const string Messages = "消息";

    private const string Operation = "操作";

    private const string Startup = "启动";

    [Category(Operation), Description("启用后，机器人将处理发送到频道的命令。"), DisplayName("允许频道命令")]
    public bool AllowCommandsViaChannel { get; set; } = true;

    [Category(Operation), Description("启用后，机器人将允许用户通过私信发送命令（绕过慢速模式）"), DisplayName("允许私信命令")]
    public bool AllowCommandsViaWhisper { get; set; }

    [Category(Startup), Description("发送消息的频道"), DisplayName("频道")]
    public string Channel { get; set; } = string.Empty;

    [Category(Startup), Description("机器人命令前缀"), DisplayName("命令前缀")]
    public char CommandPrefix { get; set; } = '$';

    [Category(Operation), Description("Discord 服务器链接。"), DisplayName("Discord 链接")]
    public string DiscordLink { get; set; } = string.Empty;

    [Category(Messages), Description("切换分发交易是否在开始前倒计时。"), DisplayName("分发倒计时")]
    public bool DistributionCountDown { get; set; } = true;

    [Category(Operation), Description("捐赠链接。"), DisplayName("捐赠链接")]
    public string DonationLink { get; set; } = string.Empty;

    [Category(Operation), Description("屏障释放时发送的消息。"), DisplayName("开始消息")]
    public string MessageStart { get; set; } = string.Empty;

    [Category(Messages), Description("确定通用通知发送到哪里。"), DisplayName("通知目的地")]
    public TwitchMessageDestination NotifyDestination { get; set; }

    [Category(Operation), Description("Sudo 用户名"), DisplayName("Sudo 列表")]
    public string SudoList { get; set; } = string.Empty;

    [Category(Operation), Description("如果在过去 Y 秒内发送了 X 条消息，则限制机器人发送消息。"), DisplayName("限流消息数")]
    public int ThrottleMessages { get; set; } = 100;

    // 消息
    [Category(Operation), Description("如果在过去 Y 秒内发送了 X 条消息，则限制机器人发送消息。"), DisplayName("限流秒数")]
    public double ThrottleSeconds { get; set; } = 30;

    [Category(Operation), Description("如果在过去 Y 秒内发送了 X 条私信，则限制机器人发送私信。"), DisplayName("限流私信数")]
    public int ThrottleWhispers { get; set; } = 100;

    [Category(Operation), Description("如果在过去 Y 秒内发送了 X 条私信，则限制机器人发送私信。"), DisplayName("限流私信秒数")]
    public double ThrottleWhispersSeconds { get; set; } = 60;

    [Category(Startup), Description("机器人登录令牌"), DisplayName("令牌")]
    public string Token { get; set; } = string.Empty;

    [Category(Messages), Description("确定交易取消通知发送到哪里。"), DisplayName("交易取消目的地")]
    public TwitchMessageDestination TradeCanceledDestination { get; set; } = TwitchMessageDestination.Channel;

    [Category(Messages), Description("确定交易完成通知发送到哪里。"), DisplayName("交易完成目的地")]
    public TwitchMessageDestination TradeFinishDestination { get; set; }

    [Category(Messages), Description("确定交易搜索通知发送到哪里。"), DisplayName("交易搜索目的地")]
    public TwitchMessageDestination TradeSearchDestination { get; set; }

    // 消息目的地
    [Category(Messages), Description("确定交易开始通知发送到哪里。"), DisplayName("交易开始目的地")]
    public TwitchMessageDestination TradeStartDestination { get; set; } = TwitchMessageDestination.Channel;

    [Category(Operation), Description("机器人使用教程链接。"), DisplayName("教程链接")]
    public string TutorialLink { get; set; } = string.Empty;

    [Category(Operation), Description("机器人使用教程文本。"), DisplayName("教程文本")]
    public string TutorialText { get; set; } = string.Empty;

    // 操作
    [Category(Operation), Description("具有这些用户名的用户无法使用机器人。"), DisplayName("用户黑名单")]
    public string UserBlacklist { get; set; } = string.Empty;

    // 启动
    [Category(Startup), Description("机器人用户名"), DisplayName("用户名")]
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
