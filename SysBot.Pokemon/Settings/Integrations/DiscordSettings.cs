using System;
using System.ComponentModel;
using static SysBot.Pokemon.TradeSettings;

namespace SysBot.Pokemon;

public class DiscordSettings
{
    private const string Channels = "频道";

    private const string Operation = "运行";

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

    [Category(Startup), DisplayName("登录 Token"), Description("机器人登录 Token。")]
    public string Token { get; set; } = string.Empty;

    [Category(Operation), DisplayName("附加嵌入文本"), Description("在嵌入描述开头附加的额外文本。")]
    public string[] AdditionalEmbedText { get; set; } = [];

    [Category(Users), DisplayName("允许全局 Sudo"), Description("禁用后将取消全局 Sudo 支持。")]
    public bool AllowGlobalSudo { get; set; } = true;

    [Category(Channels), DisplayName("公告频道列表"), Description("用于记录公告等特殊消息的频道。")]
    public RemoteControlAccessList AnnouncementChannels { get; set; } = new();

    [Category(Channels), DisplayName("滥用日志频道列表"), Description("用于记录滥用消息的频道。")]
    public RemoteControlAccessList AbuseLogChannels { get; set; } = new();

    [Category(Channels), DisplayName("公告嵌入设置"), Description("配置公告嵌入消息的详细选项。")]
    public AnnouncementSettingsCategory AnnouncementSettings { get; set; } = new();

    [Category(Startup), DisplayName("仅交易机器人彩色状态"), Description("在线状态颜色仅根据交易类型的机器人计算。")]
    public bool BotColorStatusTradeOnly { get; set; } = true;

    [Category(Startup), DisplayName("推送在线状态嵌入"), Description("向白名单频道发送在线 / 离线状态嵌入消息。")]
    public bool BotEmbedStatus { get; set; } = true;

    [Category(Startup), DisplayName("自定义状态文本"), Description("自定义正在游玩的状态文本。")]
    public string BotGameStatus { get; set; } = "Pokémon";

    [Category(Startup), DisplayName("频道状态表情"), Description("根据当前状态在频道名称中添加在线 / 离线表情，仅对白名单频道生效。")]
    public bool ChannelStatus { get; set; } = true;

    [Category(Channels), DisplayName("频道白名单"), Description("仅在这些频道 ID 中响应命令。")]
    public RemoteControlAccessList ChannelWhitelist { get; set; } = new();

    [Category(Startup), DisplayName("命令前缀"), Description("机器人命令前缀。")]
    public string CommandPrefix { get; set; } = "$";

    [Category(Operation), DisplayName("任意频道回复配置"), Description("允许机器人在可见的任何频道回复 Showdown 配置，而不仅限于白名单频道。")]
    public bool ConvertPKMReplyAnyChannel { get; set; } = false;

    [Category(Operation), DisplayName("自动转换 PKM 配置"), Description("监听频道消息，在检测到附件 PKM 文件时自动回复 Showdown 配置（无需命令）。")]
    public bool ConvertPKMToShowdownSet { get; set; } = true;

    [Category(Users), DisplayName("Sudo 用户列表"), Description("拥有 Sudo 权限的 Discord 用户 ID（以逗号分隔）。")]
    public RemoteControlAccessList GlobalSudoList { get; set; } = new();

    [Category(Operation), DisplayName("打招呼回复"), Description("当用户打招呼时机器人返回的自定义消息，可使用格式化参数引用用户。")]
    public string HelloResponse { get; set; } = "你好，{0}！";

    [Category(Channels), DisplayName("日志频道列表"), Description("回显日志数据的频道 ID。")]
    public RemoteControlAccessList LoggingChannels { get; set; } = new();

    [Category(Startup), DisplayName("禁用模块列表"), Description("启动时不加载的模块列表（用逗号分隔）。")]
    public string ModuleBlacklist { get; set; } = string.Empty;

    [Category(Startup), DisplayName("离线表情"), Description("机器人离线时使用的表情。")]
    public string OfflineEmoji { get; set; } = "❌";

    [Category(Startup), DisplayName("在线表情"), Description("机器人在线时使用的表情。")]
    public string OnlineEmoji { get; set; } = "✅";

    [Category(Operation), DisplayName("提示无命令权限"), Description("当用户在当前频道无权使用命令时给予提示；关闭后将静默忽略。")]
    public bool ReplyCannotUseCommandInChannel { get; set; } = true;

    [Category(Operation), DisplayName("回复感谢消息"), Description("在用户向机器人致谢时随机回复。")]
    public bool ReplyToThanks { get; set; } = true;

    [Category(Operation), DisplayName("返回展示过的宝可梦"), Description("在交易中将展示过的宝可梦文件返回给用户。")]
    public bool ReturnPKMs { get; set; } = true;

    [Category(Operation), DisplayName("自动删除消息"), Description("启用后，机器人会在延迟后自动删除错误消息及相关命令。关闭则保留所有消息。")]
    public bool MessageDeletionEnabled { get; set; } = true;

    [Category(Operation), DisplayName("删除延迟（秒）"), Description("删除机器人错误 / 响应消息前等待的秒数，仅在开启自动删除时生效。")]
    public int ErrorMessageDeleteDelaySeconds { get; set; } = 12;

    [Category(Operation), DisplayName("同时删除用户命令"), Description("启用后，删除机器人回应的同时也删除用户命令。关闭则保留用户命令。")]
    public bool DeleteUserCommandMessages { get; set; } = true;

    [Category(Roles), DisplayName("克隆队列角色"), Description("拥有该角色的用户可进入克隆队列。")]
    public RemoteControlAccessList RoleCanClone { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), DisplayName("放生队列角色"), Description("拥有该角色的用户可进入放生队列。")]
    public RemoteControlAccessList RoleCanDump { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), DisplayName("修复 OT 队列角色"), Description("拥有该角色的用户可进入修复 OT 队列。")]
    public RemoteControlAccessList RoleCanFixOT { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), DisplayName("种子检测 / 特殊请求角色"), Description("拥有该角色的用户可进入种子检测 / 特殊请求队列。")]
    public RemoteControlAccessList RoleCanSeedCheckorSpecialRequest { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), DisplayName("交易队列角色"), Description("拥有该角色的用户可进入交易队列。")]
    public RemoteControlAccessList RoleCanTrade { get; set; } = new() { AllowIfEmpty = true };

    [Category(Roles), DisplayName("高优先级角色"), Description("拥有该角色的用户进队列时拥有更高优先级。")]
    public RemoteControlAccessList RoleFavored { get; set; } = new() { AllowIfEmpty = false };

    // Whitelists
    [Category(Roles), DisplayName("远程控制角色"), Description("拥有该角色的用户可远程控制主机（限远控模式）。")]
    public RemoteControlAccessList RoleRemoteControl { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), DisplayName("Sudo 角色"), Description("拥有该角色的用户可绕过命令限制。")]
    public RemoteControlAccessList RoleSudo { get; set; } = new() { AllowIfEmpty = false };

    // Operation
    [Category(Servers), DisplayName("屏蔽服务器列表"), Description("处于该列表中的服务器将被拒绝使用机器人，并会被自动退群。")]
    public RemoteControlAccessList ServerBlacklist { get; set; } = new() { AllowIfEmpty = false };

    [Category(Channels), DisplayName("交易开始日志频道"), Description("记录交易开始消息的日志频道。")]
    public RemoteControlAccessList TradeStartingChannels { get; set; } = new();

    // Startup
    [Category(Users), DisplayName("屏蔽用户列表"), Description("该列表中的用户 ID 将被拒绝使用机器人。")]
    public RemoteControlAccessList UserBlacklist { get; set; } = new();

    public override string ToString() => "Discord 集成设置";

    [Category(Operation), TypeConverter(typeof(CategoryConverter<AnnouncementSettingsCategory>))]
    public class AnnouncementSettingsCategory
    {
        [Category("嵌入设置"), DisplayName("嵌入颜色"), Description("公告嵌入消息使用的颜色主题。")]
        public EmbedColorOption AnnouncementEmbedColor { get; set; } = EmbedColorOption.Purple;

        [Category("嵌入设置"), DisplayName("缩略图样式"), Description("公告使用的缩略图样式。")]
        public ThumbnailOption AnnouncementThumbnailOption { get; set; } = ThumbnailOption.Gengar;

        [Category("嵌入设置"), DisplayName("自定义缩略图 URL"), Description("公告自定义缩略图的 URL。")]
        public string CustomAnnouncementThumbnailUrl { get; set; } = string.Empty;

        [Category("嵌入设置"), DisplayName("随机颜色"), Description("启用后，公告将随机挑选颜色。")]
        public bool RandomAnnouncementColor { get; set; } = false;

        [Category("嵌入设置"), DisplayName("随机缩略图"), Description("启用后，公告将随机挑选缩略图。")]
        public bool RandomAnnouncementThumbnail { get; set; } = false;

        public override string ToString() => "公告设置";
    }
}
