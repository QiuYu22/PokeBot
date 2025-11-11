using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Console agnostic settings
/// </summary>
public abstract class BaseConfig
{
    protected const string FeatureToggle = "功能配置";

    protected const string Operation = "运行参数";

    [Browsable(false)]
    private const string Debug = "调试选项";

    [DisplayName("防休眠按键")]
    [Category(FeatureToggle), Description("启用后，机器人在空闲时会偶尔按下 B 键，防止主机进入睡眠。")]
    public bool AntiIdle { get; set; }

    [DisplayName("目录与转储设置")]
    [Description("配置分发目录与接收结果保存目录。")]
    [Category(Operation)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FolderSettings Folder { get; set; } = new();

    [DisplayName("合法性生成设置")]
    [Description("控制自动生成或修正宝可梦时的合法性策略。")]
    [Category(Operation)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public LegalitySettings Legality { get; set; } = new();

    [DisplayName("启用日志记录")]
    [Category(FeatureToggle), Description("开启文本日志记录。需重启程序后生效。")]
    public bool LoggingEnabled { get; set; } = true;

    [DisplayName("历史日志保留数量")]
    [Category(FeatureToggle), Description("保留的历史日志文件数量。设置为 ≤ 0 可关闭日志清理功能。需重启程序后生效。")]
    public int MaxArchiveFiles { get; set; } = 14;

    public abstract bool Shuffled { get; }

    [Browsable(false)]
    [DisplayName("跳过创建机器人")]
    [Category(Debug), Description("启动程序时跳过创建机器人，便于进行集成调试。")]
    public bool SkipConsoleBotCreation { get; set; }

    [DisplayName("使用键盘输入密码")]
    [Category(FeatureToggle), Description("启用后，机器人将通过键盘输入联机密码（速度更快）。")]
    public bool UseKeyboard { get; set; } = true;
}
