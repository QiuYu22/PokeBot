using System.ComponentModel;

namespace SysBot.Pokemon;

public class TradeAbuseSettings
{
    private const string Monitoring = "监控";
    public override string ToString() => "交易滥用监控设置";

    [Category(Monitoring), DisplayName("重复出现冷却（分钟）"), Description("当用户在此分钟数内再次出现，将发送通知。")]
    public double TradeCooldown { get; set; }

    [Category(Monitoring), DisplayName("冷却违例附带 Nintendo ID"), Description("启用后，当用户忽略冷却时会在回显中附带其 Nintendo Account ID。")]
    public bool EchoNintendoOnlineIDCooldown { get; set; } = true;

    [Category(Monitoring), DisplayName("冷却违例提醒附加内容"), Description("检测到冷却违例时追加到回显的文本；例如 Discord 可使用 <@userID> 提醒。")]
    public string CooldownAbuseEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), DisplayName("跨账号冷却（分钟）"), Description("当用户在该分钟数内换 Discord/Twitch 账号出现时发送通知。")]
    public double TradeAbuseExpiration { get; set; } = 120;

    [Category(Monitoring), DisplayName("多账号附带 Nintendo ID"), Description("检测到多账号使用时，回显包含 Nintendo Account ID。")]
    public bool EchoNintendoOnlineIDMulti { get; set; } = true;

    [Category(Monitoring), DisplayName("多收件附带 Nintendo ID"), Description("检测到向多个游戏账号发送时，回显包含 Nintendo Account ID。")]
    public bool EchoNintendoOnlineIDMultiRecipients { get; set; } = true;

    [Category(Monitoring), DisplayName("多账号处理动作"), Description("发现多账号使用时采取的动作。")]
    public TradeAbuseAction TradeAbuseAction { get; set; } = TradeAbuseAction.Quit;

    [Category(Monitoring), DisplayName("封锁同步至黑名单"), Description("在游戏内封锁多账号用户时，自动把其 Online ID 加入禁止列表。")]
    public bool BanIDWhenBlockingUser { get; set; } = true;

    [Category(Monitoring), DisplayName("多账号提醒附加内容"), Description("检测到多账号时追加到回显的文本；例如 Discord 可使用 <@userID> 提醒。")]
    public string MultiAbuseEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), DisplayName("多收件提醒附加内容"), Description("检测到向多个玩家发送时追加到回显的文本；例如 Discord 可使用 <@userID> 提醒。")]
    public string MultiRecipientEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), DisplayName("禁止 ID 列表"), Description("命中的在线 ID 会触发退出交易或游戏内封锁。")]
    public RemoteControlAccessList BannedIDs { get; set; } = new();

    [Category(Monitoring), DisplayName("封锁命中禁止 ID"), Description("遇到禁止 ID 时，先在游戏内封锁再退出交易。")]
    public bool BlockDetectedBannedUser { get; set; } = true;

    [Category(Monitoring), DisplayName("禁止 ID 提醒附加内容"), Description("检测到禁止 ID 时追加到回显的文本；例如 Discord 可使用 <@userID> 提醒。")]
    public string BannedIDMatchEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), DisplayName("Ledy 滥用附带 Nintendo ID"), Description("检测到 Ledy 昵称滥用时，回显包含 Nintendo Account ID。")]
    public bool EchoNintendoOnlineIDLedy { get; set; } = true;

    [Category(Monitoring), DisplayName("Ledy 滥用提醒附加内容"), Description("检测到 Ledy 交易违规时追加到回显的文本；例如 Discord 可使用 <@userID> 提醒。")]
    public string LedyAbuseEchoMention { get; set; } = string.Empty;
}
