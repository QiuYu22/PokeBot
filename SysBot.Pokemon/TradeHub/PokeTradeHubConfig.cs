using System.ComponentModel;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon;

public sealed class PokeTradeHubConfig : BaseConfig
{
    [Browsable(false)]
    private const string BotEncounter = "遭遇机器人";

    private const string BotTrade = "交易机器人";

    private const string Integration = "集成";

    [Category(BotTrade), Description("程序运行的 Discord 机器人名称。这将作为窗口标题以便于识别。需要重启程序。"), DisplayName("机器人名称")]
    public string BotName { get; set; } = string.Empty;

    [Category(Integration), DisplayName("Discord")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public DiscordSettings Discord { get; set; } = new();

    [Category(BotTrade), Description("空闲分发交易的设置。"), DisplayName("分发")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public DistributionSettings Distribution { get; set; } = new();

    // Encounter Bots - For finding or hosting Pokémon in-game.
    [Browsable(false)]
    [Category(BotEncounter), DisplayName("遭遇 SWSH")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public EncounterSettings EncounterSWSH { get; set; } = new();

    [Category(Integration), Description("允许优先用户以比普通用户更有利的位置加入队列。"), DisplayName("优先用户")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FavoredPrioritySettings Favoritism { get; set; } = new();

    [Category(Operation), DisplayName("队列")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public QueueSettings Queues { get; set; } = new();

    [Browsable(false)]
    [Category(BotEncounter), DisplayName("团体战 SWSH")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public RaidSettings RaidSWSH { get; set; } = new();

    [Browsable(false)]
    [Category(BotTrade), DisplayName("种子检查 SWSH")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public SeedCheckSettings SeedCheckSWSH { get; set; } = new();

    [Browsable(false)]
    public override bool Shuffled => Distribution.Shuffled;

    [Browsable(false)]
    [Category(BotEncounter), Description("遭遇机器人的停止条件。"), DisplayName("停止条件")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public StopConditionSettings StopConditions { get; set; } = new();

    [Category(Integration), Description("配置直播素材的生成。"), DisplayName("直播")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public StreamSettings Stream { get; set; } = new();

    [Browsable(false)]
    [Category(Integration), Description("用户主题选项选择。"), DisplayName("主题选项")]
    public string ThemeOption { get; set; } = string.Empty;

    [Category(Operation), Description("为较慢的 Switch 添加额外时间。"), DisplayName("时间设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TimingSettings Timings { get; set; } = new();

    // Trade Bots

    [Category(BotTrade), DisplayName("交易")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TradeSettings Trade { get; set; } = new();

    [Category(BotTrade), DisplayName("交易滥用")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TradeAbuseSettings TradeAbuse { get; set; } = new();

    // Integration
    [Category(Integration), DisplayName("Twitch")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TwitchSettings Twitch { get; set; } = new();

    [Category(Integration), DisplayName("YouTube")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public YouTubeSettings YouTube { get; set; } = new();

    [Category(Operation), Description("崩溃后自动恢复机器人的设置。"), DisplayName("恢复")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public RecoverySettings Recovery { get; set; } = new();

    [Category(Integration), Description("Web 控制面板服务器的设置。"), DisplayName("Web 服务器")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public WebServerSettings WebServer { get; set; } = new();
}
