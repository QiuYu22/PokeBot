using System;
using System.ComponentModel;
using static SysBot.Pokemon.TradeSettings;

namespace SysBot.Pokemon;

public class DiscordSettings
{
    private const string Channels = nameof(Channels);

    private const string Operation = nameof(Operation);

    private const string Roles = nameof(Roles);

    private const string Servers = nameof(Servers);

    private const string Startup = nameof(Startup);

    private const string Users = nameof(Users);

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

    [Category(Startup), DisplayName("机器人令牌"), Description("机器人登录 Token。")]
    public string Token { get; set; } = string.Empty;

    [Category(Operation), DisplayName("附加嵌入文本"), Description("在嵌入描述开头追加的额外文本。")]
    public string[] AdditionalEmbedText { get; set; } = [];

    [Category(Users), DisplayName("允许全局管理员权限"), Description("禁用后将移除全局管理员权限支持。")]
    public bool AllowGlobalSudo { get; set; } = true;

    [Category(Channels), DisplayName("公告记录频道"), Description("用于记录公告等特殊消息的频道。")]
    public RemoteControlAccessList AnnouncementChannels { get; set; } = new();

    [Category(Channels), DisplayName("滥用日志频道"), Description("用于记录滥用消息的频道。")]
    public RemoteControlAccessList AbuseLogChannels { get; set; } = new();

    [Category(Operation), DisplayName("公告设置"), Description("自定义公告嵌入颜色与缩略图的选项。")]
    public AnnouncementSettingsCategory AnnouncementSettings { get; set; } = new();

    [Category(Startup), DisplayName("仅贸易机器人状态颜色"), Description("仅依据贸易类型的机器人来决定 Discord 状态颜色。")]
    public bool BotColorStatusTradeOnly { get; set; } = true;

    [Category(Startup), DisplayName("状态嵌入推送"), Description("向所有白名单频道发送在线/离线状态嵌入。")]
    public bool BotEmbedStatus { get; set; } = true;

    [Category(Startup), DisplayName("自定义游戏状态"), Description("设置“正在游玩”字段的自定义状态。")]
    public string BotGameStatus { get; set; } = "Pokémon";

    [Category(Startup), DisplayName("频道状态表情"), Description("依据当前状态在白名单频道名称中添加在线/离线表情。")]
    public bool ChannelStatus { get; set; } = true;

    [Category(Channels), DisplayName("频道白名单"), Description("只有这些频道 ID 会被机器人识别并响应命令。")]
    public RemoteControlAccessList ChannelWhitelist { get; set; } = new();

    [Category(Startup), DisplayName("命令前缀"), Description("机器人可识别的命令前缀。")]
    public string CommandPrefix { get; set; } = "$";

    [Category(Operation), DisplayName("任意频道返回 Showdown"), Description("允许机器人在任意可见频道返回 ShowdownSet，而不受白名单限制。")]
    public bool ConvertPKMReplyAnyChannel { get; set; } = false;

    [Category(Operation), DisplayName("自动转换 PKM"), Description("监听带有 PKM 附件的消息并自动回复 ShowdownSet。")]
    public bool ConvertPKMToShowdownSet { get; set; } = true;

    [Category(Users), DisplayName("全局管理员权限列表"), Description("逗号分隔的 Discord 用户 ID，拥有 Bot Hub 的管理员权限访问。")]
    public RemoteControlAccessList GlobalSudoList { get; set; } = new();

    [Category(Operation), DisplayName("问候回复"), Description("自定义当用户向机器人问好时的回复，可使用字符串格式化以提及用户。")]
    public string HelloResponse { get; set; } = "Hi {0}!";

    [Category(Channels), DisplayName("日志回显频道"), Description("用于回显机器人日志数据的频道 ID。")]
    public RemoteControlAccessList LoggingChannels { get; set; } = new();

    [Category(Startup), DisplayName("模块黑名单"), Description("启动时不加载的模块列表（逗号分隔）。")]
    public string ModuleBlacklist { get; set; } = string.Empty;

    [Category(Startup), DisplayName("离线表情"), Description("机器人离线时使用的自定义表情。")]
    public string OfflineEmoji { get; set; } = "❌";

    [Category(Startup), DisplayName("在线表情"), Description("机器人在线时使用的自定义表情。")]
    public string OnlineEmoji { get; set; } = "✅";

    [Category(Operation), DisplayName("频道权限提示"), Description("当用户无权在频道使用命令时给予提示；禁用后将静默忽略。")]
    public bool ReplyCannotUseCommandInChannel { get; set; } = true;

    [Category(Operation), DisplayName("回应感谢"), Description("用户感谢机器人时随机发送回复。")]
    public bool ReplyToThanks { get; set; } = true;

    [Category(Operation), DisplayName("返还 PKM"), Description("将交易中展示的宝可梦 PKM 文件返还给用户。")]
    public bool ReturnPKMs { get; set; } = true;

    [Category(Operation), DisplayName("启用消息删除"), Description("启用后机器人会延迟删除错误消息及用户命令，禁用则永久保留。")]
    public bool MessageDeletionEnabled { get; set; } = true;

    [Category(Operation), DisplayName("错误消息延迟秒"), Description("删除机器人错误/响应消息前等待的秒数，仅在启用消息删除时生效。")]
    public int ErrorMessageDeleteDelaySeconds { get; set; } = 12;

    [Category(Operation), DisplayName("删除用户命令"), Description("启用后删除机器人回复时会一同删除用户命令，禁用则保留。")]
    public bool DeleteUserCommandMessages { get; set; } = true;

    [Category(Roles), DisplayName("克隆队列角色"), Description("拥有此角色的用户可以进入克隆队列。")]
    public RemoteControlAccessList RoleCanClone { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), DisplayName("导出队列角色"), Description("拥有此角色的用户可以进入导出队列。")]
    public RemoteControlAccessList RoleCanDump { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), DisplayName("修复OT队列角色"), Description("拥有此角色的用户可以进入 FixOT 队列。")]
    public RemoteControlAccessList RoleCanFixOT { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), DisplayName("种子/特殊队列角色"), Description("拥有此角色的用户可以进入种子检查或特殊请求队列。")]
    public RemoteControlAccessList RoleCanSeedCheckorSpecialRequest { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), DisplayName("交易队列角色"), Description("拥有此角色的用户可以进入交易队列。")]
    public RemoteControlAccessList RoleCanTrade { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), DisplayName("优先队列角色"), Description("拥有此角色的用户可以以更优位置加入队列。")]
    public RemoteControlAccessList RoleFavored { get; set; } = new() { AllowIfEmpty = false };

    // Whitelists
    [Category(Roles), DisplayName("远程控制角色"), Description("拥有此角色的用户可远程控制主机（作为远程控制机器人时）。")]
    public RemoteControlAccessList RoleRemoteControl { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), DisplayName("管理员权限角色"), Description("拥有此角色的用户可绕过命令限制。")]
    public RemoteControlAccessList RoleSudo { get; set; } = new() { AllowIfEmpty = false };

    // Operation
    [Category(Servers), DisplayName("服务器黑名单"), Description("这些服务器 ID 无法使用机器人，机器人会离开该服务器。")]
    public RemoteControlAccessList ServerBlacklist { get; set; } = new() { AllowIfEmpty = false };

    [Category(Channels), DisplayName("交易开始频道"), Description("记录交易开始消息的日志频道。")]
    public RemoteControlAccessList TradeStartingChannels { get; set; } = new();

    // Startup
    [Category(Users), DisplayName("用户黑名单"), Description("这些用户 ID 无法使用机器人。")]
    public RemoteControlAccessList UserBlacklist { get; set; } = new();

    public override string ToString() => "Discord 集成设置";

    [Category(Operation), TypeConverter(typeof(CategoryConverter<AnnouncementSettingsCategory>))]
    public class AnnouncementSettingsCategory
    {
        [DisplayName("公告嵌入颜色"), Description("设置公告嵌入使用的颜色。")]
        public EmbedColorOption AnnouncementEmbedColor { get; set; } = EmbedColorOption.Purple;

        [Category("Embed Settings"), DisplayName("公告缩略图"), Description("公告使用的缩略图预设选项。")]
        public ThumbnailOption AnnouncementThumbnailOption { get; set; } = ThumbnailOption.Gengar;

        [Category("Embed Settings"), DisplayName("自定义缩略图 URL"), Description("公告缩略图的自定义 URL。")]
        public string CustomAnnouncementThumbnailUrl { get; set; } = string.Empty;

        [Category("Embed Settings"), DisplayName("随机公告颜色"), Description("启用后公告嵌入颜色将随机选择。")]
        public bool RandomAnnouncementColor { get; set; } = false;

        [Category("Embed Settings"), DisplayName("随机公告缩略图"), Description("启用后公告缩略图将在预设中随机选择。")]
        public bool RandomAnnouncementThumbnail { get; set; } = false;

        public override string ToString() => "公告设置";
    }
}
