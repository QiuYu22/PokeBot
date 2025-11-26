using System;
using System.ComponentModel;
using static SysBot.Pokemon.TradeSettings;

namespace SysBot.Pokemon;

public class DiscordSettings
{
    private const string Channels = "频道";

    private const string Operation = "操作";

    private const string Roles = "角色";

    private const string Servers = "服务器";

    private const string Startup = "启动";

    private const string Users = "用户";

    public enum EmbedColorOption
    {
        Blue,

        Green,

        Red,

        Gold,

        Purple,

        Teal,

        Orange,

        Magenta,

        LightGrey,

        DarkGrey
    }

    public enum ThumbnailOption
    {
        Gengar,

        Pikachu,

        Umbreon,

        Sylveon,

        Charmander,

        Jigglypuff,

        Flareon,

        Custom
    }

    [Category(Startup), Description("机器人登录令牌。"), DisplayName("令牌")]
    public string Token { get; set; } = string.Empty;

    [Category(Operation), Description("添加到嵌入描述开头的额外文本。"), DisplayName("额外嵌入文本")]
    public string[] AdditionalEmbedText { get; set; } = [];

    [Category(Users), Description("禁用此选项将移除全局 sudo 支持。"), DisplayName("允许全局 Sudo")]
    public bool AllowGlobalSudo { get; set; } = true;

    [Category(Channels), Description("将记录特殊消息（如公告）的频道。"), DisplayName("公告频道")]
    public RemoteControlAccessList AnnouncementChannels { get; set; } = new();

    [Category(Channels), Description("将记录滥用消息的频道。"), DisplayName("滥用日志频道")]
    public RemoteControlAccessList AbuseLogChannels { get; set; } = new();

    [Category(Channels), Description("公告相关设置。"), DisplayName("公告设置")]
    public AnnouncementSettingsCategory AnnouncementSettings { get; set; } = new();

    [Category(Startup), Description("仅考虑交易类型的机器人来显示 Discord 在线状态颜色。"), DisplayName("仅交易机器人状态颜色")]
    public bool BotColorStatusTradeOnly { get; set; } = true;

    [Category(Startup), Description("将向所有白名单频道发送在线/离线状态嵌入。"), DisplayName("机器人嵌入状态")]
    public bool BotEmbedStatus { get; set; } = true;

    [Category(Startup), Description("游戏状态的自定义文本。"), DisplayName("机器人游戏状态")]
    public string BotGameStatus { get; set; } = "Pokémon";

    [Category(Startup), Description("根据当前状态在频道名称中添加在线/离线表情符号。仅限白名单频道。"), DisplayName("频道状态")]
    public bool ChannelStatus { get; set; } = true;

    [Category(Channels), Description("具有这些 ID 的频道是机器人唯一响应命令的频道。"), DisplayName("频道白名单")]
    public RemoteControlAccessList ChannelWhitelist { get; set; } = new();

    [Category(Startup), Description("机器人命令前缀。"), DisplayName("命令前缀")]
    public string CommandPrefix { get; set; } = "$";

    [Category(Operation), Description("机器人可以在任何它能看到的频道中回复 ShowdownSet，而不仅仅是被列入白名单的频道。只有在你希望机器人在非机器人频道中提供更多功能时才启用此选项。"), DisplayName("任意频道回复 PKM")]
    public bool ConvertPKMReplyAnyChannel { get; set; } = false;

    [Category(Operation), Description("机器人监听频道消息，当有 PKM 文件附件时（不是通过命令）会回复 ShowdownSet。"), DisplayName("PKM 转 Showdown")]
    public bool ConvertPKMToShowdownSet { get; set; } = true;

    [Category(Users), Description("将拥有 Bot Hub sudo 访问权限的 Discord 用户 ID（逗号分隔）。"), DisplayName("全局 Sudo 列表")]
    public RemoteControlAccessList GlobalSudoList { get; set; } = new();

    [Category(Operation), Description("当用户向机器人打招呼时，机器人将回复的自定义消息。使用字符串格式化来在回复中提及用户。"), DisplayName("问候回复")]
    public string HelloResponse { get; set; } = "你好 {0}！";

    [Category(Channels), Description("将回显日志机器人数据的频道 ID。"), DisplayName("日志频道")]
    public RemoteControlAccessList LoggingChannels { get; set; } = new();

    [Category(Startup), Description("启动机器人时不会加载的模块列表（逗号分隔）。"), DisplayName("模块黑名单")]
    public string ModuleBlacklist { get; set; } = string.Empty;

    [Category(Startup), Description("机器人离线时使用的自定义表情符号。"), DisplayName("离线表情")]
    public string OfflineEmoji { get; set; } = "❌";

    [Category(Startup), Description("机器人在线时使用的自定义表情符号。"), DisplayName("在线表情")]
    public string OnlineEmoji { get; set; } = "✅";

    [Category(Operation), Description("当用户不被允许在频道中使用给定命令时回复用户。设置为 false 时，机器人将静默忽略他们。"), DisplayName("回复无权限命令")]
    public bool ReplyCannotUseCommandInChannel { get; set; } = true;

    [Category(Operation), Description("将向感谢机器人的用户发送随机回复。"), DisplayName("回复感谢")]
    public bool ReplyToThanks { get; set; } = true;

    [Category(Operation), Description("将交易中展示的宝可梦的 PKM 文件返回给用户。"), DisplayName("返回 PKM 文件")]
    public bool ReturnPKMs { get; set; } = true;

    [Category(Operation), Description("启用后，机器人将在延迟后自动删除错误消息和用户命令。禁用以永久保留所有消息。"), DisplayName("启用消息删除")]
    public bool MessageDeletionEnabled { get; set; } = true;

    [Category(Operation), Description("删除机器人错误/响应消息前等待的秒数。仅在 MessageDeletionEnabled 为 true 时适用。"), DisplayName("错误消息删除延迟")]
    public int ErrorMessageDeleteDelaySeconds { get; set; } = 12;

    [Category(Operation), Description("启用后，用户命令消息将与机器人响应一起删除。禁用以保持用户命令可见。"), DisplayName("删除用户命令消息")]
    public bool DeleteUserCommandMessages { get; set; } = true;

    [Category(Roles), Description("拥有此角色的用户可以进入克隆队列。"), DisplayName("克隆角色")]
    public RemoteControlAccessList RoleCanClone { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), Description("拥有此角色的用户可以进入转储队列。"), DisplayName("转储角色")]
    public RemoteControlAccessList RoleCanDump { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), Description("拥有此角色的用户可以进入 FixOT 队列。"), DisplayName("FixOT 角色")]
    public RemoteControlAccessList RoleCanFixOT { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), Description("拥有此角色的用户可以进入种子检查/特殊请求队列。"), DisplayName("种子检查角色")]
    public RemoteControlAccessList RoleCanSeedCheckorSpecialRequest { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), Description("拥有此角色的用户可以进入交易队列。"), DisplayName("交易角色")]
    public RemoteControlAccessList RoleCanTrade { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), Description("拥有此角色的用户可以以更好的位置加入队列。"), DisplayName("优先角色")]
    public RemoteControlAccessList RoleFavored { get; set; } = new() { AllowIfEmpty = false };

    // 白名单
    [Category(Roles), Description("拥有此角色的用户可以远程控制主机（如果作为远程控制机器人运行）。"), DisplayName("远程控制角色")]
    public RemoteControlAccessList RoleRemoteControl { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("拥有此角色的用户可以绕过命令限制。"), DisplayName("Sudo 角色")]
    public RemoteControlAccessList RoleSudo { get; set; } = new() { AllowIfEmpty = false };

    // 操作
    [Category(Servers), Description("具有这些 ID 的服务器将无法使用机器人，机器人将离开该服务器。"), DisplayName("服务器黑名单")]
    public RemoteControlAccessList ServerBlacklist { get; set; } = new() { AllowIfEmpty = false };

    [Category(Channels), Description("将记录交易开始消息的日志频道。"), DisplayName("交易开始频道")]
    public RemoteControlAccessList TradeStartingChannels { get; set; } = new();

    // 启动
    [Category(Users), Description("具有这些用户 ID 的用户无法使用机器人。"), DisplayName("用户黑名单")]
    public RemoteControlAccessList UserBlacklist { get; set; } = new();

    public override string ToString() => "Discord 集成设置";

    [Category(Operation), TypeConverter(typeof(CategoryConverter<AnnouncementSettingsCategory>))]
    public class AnnouncementSettingsCategory
    {
        [Description("公告嵌入颜色。"), DisplayName("公告嵌入颜色")]
        public EmbedColorOption AnnouncementEmbedColor { get; set; } = EmbedColorOption.Purple;

        [Category("嵌入设置"), Description("公告的缩略图选项。"), DisplayName("公告缩略图选项")]
        public ThumbnailOption AnnouncementThumbnailOption { get; set; } = ThumbnailOption.Gengar;

        [Category("嵌入设置"), Description("公告的自定义缩略图 URL。"), DisplayName("自定义公告缩略图 URL")]
        public string CustomAnnouncementThumbnailUrl { get; set; } = string.Empty;

        [Category("嵌入设置"), Description("为公告启用随机颜色选择。"), DisplayName("随机公告颜色")]
        public bool RandomAnnouncementColor { get; set; } = false;

        [Category("嵌入设置"), Description("为公告启用随机缩略图选择。"), DisplayName("随机公告缩略图")]
        public bool RandomAnnouncementThumbnail { get; set; } = false;

        public override string ToString() => "公告设置";
    }
}
