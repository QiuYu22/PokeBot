using System.ComponentModel;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon;

public sealed class PokeTradeHubConfig : BaseConfig
{
    [Browsable(false)]
    private const string BotEncounter = "遭遇机器人";

    private const string BotTrade = "交易机器人";

    private const string Integration = "整合";

    [Category(BotTrade), Description("程序正在运行的 Discord 机器人名称，将作为窗口标题以便识别，修改后需重启程序。")]
    [DisplayName("机器人名称")]
    public string BotName { get; set; } = string.Empty;

    [Category(Integration)]
    [DisplayName("Discord 设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public DiscordSettings Discord { get; set; } = new();

    [Category(BotTrade), Description("闲置派发交易相关设置。")]
    [DisplayName("派发设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public DistributionSettings Distribution { get; set; } = new();

    // Encounter Bots - For finding or hosting Pokémon in-game.
    [Browsable(false)]
    [Category(BotEncounter)]
    [DisplayName("剑盾遭遇设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public EncounterSettings EncounterSWSH { get; set; } = new();

    [Category(Integration), Description("允许优先用户以更有利的位置加入队列。")]
    [DisplayName("优先队列设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FavoredPrioritySettings Favoritism { get; set; } = new();

    [Category(Operation)]
    [DisplayName("队列设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public QueueSettings Queues { get; set; } = new();

    [Browsable(false)]
    [Category(BotEncounter)]
    [DisplayName("剑盾团战设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public RaidSettings RaidSWSH { get; set; } = new();

    [Browsable(false)]
    [Category(BotTrade)]
    [DisplayName("剑盾种子检测设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public SeedCheckSettings SeedCheckSWSH { get; set; } = new();

    [Browsable(false)]
    [DisplayName("派发随机化")]
    public override bool Shuffled => Distribution.Shuffled;

    [Browsable(false)]
    [Category(BotEncounter), Description("EncounterBot 的停止条件。")]
    [DisplayName("停止条件设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public StopConditionSettings StopConditions { get; set; } = new();

    [Category(Integration), Description("配置直播素材生成。")]
    [DisplayName("直播素材设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public StreamSettings Stream { get; set; } = new();

    [Browsable(false)]
    [Category(Integration), Description("用户选择的主题选项。")]
    [DisplayName("主题选项")]
    public string ThemeOption { get; set; } = string.Empty;

    [Category(Operation), Description("为较慢的主机增加额外时间。")]
    [DisplayName("时间设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TimingSettings Timings { get; set; } = new();

    // Trade Bots

    [Category(BotTrade)]
    [DisplayName("交易设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TradeSettings Trade { get; set; } = new();

    [Category(BotTrade)]
    [DisplayName("交易滥用设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TradeAbuseSettings TradeAbuse { get; set; } = new();

    // Integration
    [Category(Integration)]
    [DisplayName("Twitch 设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TwitchSettings Twitch { get; set; } = new();

    [Category(Integration)]
    [DisplayName("YouTube 设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public YouTubeSettings YouTube { get; set; } = new();

    [Category(Operation), Description("崩溃后自动恢复机器人相关设置。")]
    [DisplayName("恢复设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public RecoverySettings Recovery { get; set; } = new();

    [Category(Integration), Description("Web 控制面板服务器设置。")]
    [DisplayName("Web 服务器设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public WebServerSettings WebServer { get; set; } = new();
}
