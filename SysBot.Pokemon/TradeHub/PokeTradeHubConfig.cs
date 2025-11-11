using System.ComponentModel;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon;

public sealed class PokeTradeHubConfig : BaseConfig
{
    [Browsable(false)]
    private const string BotEncounter = "遭遇机器人";

    private const string BotTrade = "交易机器人";

    private const string Integration = "外部集成";

    [DisplayName("机器人名称")]
    [Category(BotTrade), Description("用于窗口标题的机器人名称，修改后需重新启动程序。")]
    public string BotName { get; set; } = string.Empty;

    [Category(Integration)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("Discord 设置")]
    public DiscordSettings Discord { get; set; } = new();

    [Category(BotTrade), Description("闲置时执行分发交易的相关设置。")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("分发设置")]
    public DistributionSettings Distribution { get; set; } = new();

    // Encounter Bots - For finding or hosting Pokémon in-game.
    [Browsable(false)]
    [Category(BotEncounter)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public EncounterSettings EncounterSWSH { get; set; } = new();

    [Category(Integration), Description("允许优先用户以更靠前的位置加入队列。")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("优先级设置")]
    public FavoredPrioritySettings Favoritism { get; set; } = new();

    [Category(Operation)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("队列设置")]
    public QueueSettings Queues { get; set; } = new();

    [Browsable(false)]
    [Category(BotEncounter)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public RaidSettings RaidSWSH { get; set; } = new();

    [Browsable(false)]
    [Category(BotTrade)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public SeedCheckSettings SeedCheckSWSH { get; set; } = new();

    [Browsable(false)]
    public override bool Shuffled => Distribution.Shuffled;

    [Browsable(false)]
    [Category(BotEncounter), Description("遭遇机器人的停止条件配置。")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public StopConditionSettings StopConditions { get; set; } = new();

    [Category(Integration), Description("配置直播素材生成相关内容。")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("直播素材")]
    public StreamSettings Stream { get; set; } = new();

    [Browsable(false)]
    [Category(Integration), Description("用户可选的主题方案。")]
    public string ThemeOption { get; set; } = string.Empty;

    [Category(Operation), Description("为运行较慢的主机额外增加等待时间。")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("时间延迟")]
    public TimingSettings Timings { get; set; } = new();

    // Trade Bots

    [Category(BotTrade)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("交易设置")]
    public TradeSettings Trade { get; set; } = new();

    [Category(BotTrade)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("交易滥用防护")]
    public TradeAbuseSettings TradeAbuse { get; set; } = new();

    // Integration
    [Category(Integration)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("Twitch 设置")]
    public TwitchSettings Twitch { get; set; } = new();

    [Category(Integration)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("YouTube 设置")]
    public YouTubeSettings YouTube { get; set; } = new();

    [Category(Operation), Description("机器人崩溃后的自动恢复设置。")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("自动恢复设置")]
    public RecoverySettings Recovery { get; set; } = new();

    [Category(Integration), Description("Web 控制面板服务器的相关设置。")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [DisplayName("Web 控制面板")]
    public WebServerSettings WebServer { get; set; } = new();
}
