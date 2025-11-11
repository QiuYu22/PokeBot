using System.ComponentModel;

namespace SysBot.Pokemon;

public class TradeAbuseSettings
{
    private const string Monitoring = "滥用监控";
    public override string ToString() => "交易滥用监控";

    [DisplayName("交易冷却时间（分钟）")]
    [Category(Monitoring), Description("同一用户再次出现低于该时长（分钟）时触发提醒。")]
    public double TradeCooldown { get; set; }

    [DisplayName("记录冷却违规账号")]
    [Category(Monitoring), Description("当用户忽略交易冷却时，在提醒中附上其任天堂账号 ID。")]
    public bool EchoNintendoOnlineIDCooldown { get; set; } = true;

    [DisplayName("冷却违规提醒附加文本")]
    [Category(Monitoring), Description("为冷却违规提醒附加额外文本，可用于 @ 指定人员。")]
    public string CooldownAbuseEchoMention { get; set; } = string.Empty;

    [DisplayName("多账号检测窗口（分钟）")]
    [Category(Monitoring), Description("在该时间窗口内出现不同 Discord/Twitch 账号时触发提醒。")]
    public double TradeAbuseExpiration { get; set; } = 120;

    [DisplayName("记录多账号 ID")]
    [Category(Monitoring), Description("检测到使用多个 Discord/Twitch 账号时，在提醒中附上任天堂账号 ID。")]
    public bool EchoNintendoOnlineIDMulti { get; set; } = true;

    [DisplayName("记录多接收账号 ID")]
    [Category(Monitoring), Description("检测到向多个游戏账号发送时，在提醒中附上任天堂账号 ID。")]
    public bool EchoNintendoOnlineIDMultiRecipients { get; set; } = true;

    [DisplayName("多账号处理方式")]
    [Category(Monitoring), Description("检测到多个 Discord/Twitch 账号时采取的操作。")]
    public TradeAbuseAction TradeAbuseAction { get; set; } = TradeAbuseAction.Quit;

    [DisplayName("封禁时记录 ID")]
    [Category(Monitoring), Description("当因多个账号被封禁时，将其联机 ID 添加到黑名单。")]
    public bool BanIDWhenBlockingUser { get; set; } = true;

    [DisplayName("多账号提醒附加文本")]
    [Category(Monitoring), Description("为多账号违规提醒附加额外文本，可用于 @ 指定人员。")]
    public string MultiAbuseEchoMention { get; set; } = string.Empty;

    [DisplayName("多接收提醒附加文本")]
    [Category(Monitoring), Description("为多接收违规提醒附加额外文本，可用于 @ 指定人员。")]
    public string MultiRecipientEchoMention { get; set; } = string.Empty;

    [DisplayName("黑名单 ID")]
    [Category(Monitoring), Description("触发强制退出或封禁的联机 ID 列表。")]
    public RemoteControlAccessList BannedIDs { get; set; } = new();

    [DisplayName("遇到黑名单时封禁")]
    [Category(Monitoring), Description("遇到黑名单 ID 时在退出交易前在游戏内封禁对方。")]
    public bool BlockDetectedBannedUser { get; set; } = true;

    [DisplayName("黑名单提醒附加文本")]
    [Category(Monitoring), Description("为黑名单匹配提醒附加额外文本，可用于 @ 指定人员。")]
    public string BannedIDMatchEchoMention { get; set; } = string.Empty;

    [DisplayName("记录 Ledy 滥用 ID")]
    [Category(Monitoring), Description("检测到 Ledy 昵称交换滥用时，在提醒中附上任天堂账号 ID。")]
    public bool EchoNintendoOnlineIDLedy { get; set; } = true;

    [DisplayName("Ledy 滥用提醒附加文本")]
    [Category(Monitoring), Description("为 Ledy 规则违规提醒附加额外文本，可用于 @ 指定人员。")]
    public string LedyAbuseEchoMention { get; set; } = string.Empty;
}
