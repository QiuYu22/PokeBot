using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Console agnostic settings
/// </summary>
public abstract class BaseConfig
{
    protected const string FeatureToggle = "功能开关";

    protected const string Operation = "操作";

    [Browsable(false)]
    private const string Debug = "调试";

    [Category(FeatureToggle), Description("启用后，机器人在不处理任何事情时会偶尔按 B 按钮（以避免休眠）。"), DisplayName("防休眠")]
    public bool AntiIdle { get; set; }

    [Category(Operation), DisplayName("文件夹")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FolderSettings Folder { get; set; } = new();

    [Category(Operation), DisplayName("合法性")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public LegalitySettings Legality { get; set; } = new();

    [Category(FeatureToggle), Description("启用文本日志。需要重启以应用更改。"), DisplayName("启用日志")]
    public bool LoggingEnabled { get; set; } = true;

    [Category(FeatureToggle), Description("要保留的旧文本日志文件的最大数量。设置为 <= 0 以禁用日志清理。需要重启以应用更改。"), DisplayName("最大存档文件数")]
    public int MaxArchiveFiles { get; set; } = 14;

    public abstract bool Shuffled { get; }

    [Browsable(false)]
    [Category(Debug), Description("启动程序时跳过创建机器人；对测试集成很有帮助。"), DisplayName("跳过创建机器人")]
    public bool SkipConsoleBotCreation { get; set; }
}
