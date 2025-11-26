using System.ComponentModel;

namespace SysBot.Pokemon;

public class TradeAbuseSettings
{
    private const string Monitoring = "监控";
    public override string ToString() => "交易滥用监控设置";

    [Category(Monitoring), Description("当一个人在少于此设置值（分钟）的时间内再次出现时，将发送通知。"), DisplayName("交易冷却时间")]
    public double TradeCooldown { get; set; }

    [Category(Monitoring), Description("当有人无视交易冷却时间时，回显消息将包含其任天堂账户 ID。"), DisplayName("冷却时回显任天堂 ID")]
    public bool EchoNintendoOnlineIDCooldown { get; set; } = true;

    [Category(Monitoring), Description("如果不为空，当用户违反交易冷却时间时，提供的字符串将附加到回显警报中以通知您指定的人。对于 Discord，使用 <@用户ID数字> 来提及。"), DisplayName("冷却滥用提及")]
    public string CooldownAbuseEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("当一个人在少于此设置值（分钟）的时间内使用不同的 Discord/Twitch 账户出现时，将发送通知。"), DisplayName("交易滥用过期时间")]
    public double TradeAbuseExpiration { get; set; } = 120;

    [Category(Monitoring), Description("当检测到有人使用多个 Discord/Twitch 账户时，回显消息将包含其任天堂账户 ID。"), DisplayName("多账户时回显任天堂 ID")]
    public bool EchoNintendoOnlineIDMulti { get; set; } = true;

    [Category(Monitoring), Description("当检测到有人向多个游戏内账户发送时，回显消息将包含其任天堂账户 ID。"), DisplayName("多接收者时回显任天堂 ID")]
    public bool EchoNintendoOnlineIDMultiRecipients { get; set; } = true;

    [Category(Monitoring), Description("当检测到有人使用多个 Discord/Twitch 账户时，执行此操作。"), DisplayName("交易滥用操作")]
    public TradeAbuseAction TradeAbuseAction { get; set; } = TradeAbuseAction.Quit;

    [Category(Monitoring), Description("当有人因多账户被游戏内封禁时，其在线 ID 将被添加到禁止列表。"), DisplayName("封禁用户时添加 ID")]
    public bool BanIDWhenBlockingUser { get; set; } = true;

    [Category(Monitoring), Description("如果不为空，当发现用户使用多个账户时，提供的字符串将附加到回显警报中以通知您指定的人。对于 Discord，使用 <@用户ID数字> 来提及。"), DisplayName("多账户滥用提及")]
    public string MultiAbuseEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("如果不为空，当发现用户向多个游戏内玩家发送时，提供的字符串将附加到回显警报中以通知您指定的人。对于 Discord，使用 <@用户ID数字> 来提及。"), DisplayName("多接收者提及")]
    public string MultiRecipientEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("将触发交易退出或游戏内封禁的被禁止的在线 ID。"), DisplayName("禁止 ID 列表")]
    public RemoteControlAccessList BannedIDs { get; set; } = new();

    [Category(Monitoring), Description("当遇到被禁止 ID 的人时，在退出交易前在游戏内封禁他们。"), DisplayName("封禁检测到的禁止用户")]
    public bool BlockDetectedBannedUser { get; set; } = true;

    [Category(Monitoring), Description("如果不为空，当用户匹配被禁止的 ID 时，提供的字符串将附加到回显警报中以通知您指定的人。对于 Discord，使用 <@用户ID数字> 来提及。"), DisplayName("禁止 ID 匹配提及")]
    public string BannedIDMatchEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("当检测到使用 Ledy 昵称交换的人滥用时，回显消息将包含其任天堂账户 ID。"), DisplayName("Ledy 滥用时回显任天堂 ID")]
    public bool EchoNintendoOnlineIDLedy { get; set; } = true;

    [Category(Monitoring), Description("如果不为空，当用户违反 Ledy 交易规则时，提供的字符串将附加到回显警报中以通知您指定的人。对于 Discord，使用 <@用户ID数字> 来提及。"), DisplayName("Ledy 滥用提及")]
    public string LedyAbuseEchoMention { get; set; } = string.Empty;
}
