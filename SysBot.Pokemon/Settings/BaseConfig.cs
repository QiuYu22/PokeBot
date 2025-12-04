using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Console agnostic settings
/// </summary>
public abstract class BaseConfig
{
    protected const string FeatureToggle = "功能开关";

    protected const string Operation = "运行";

    [Browsable(false)]
    private const string Debug = "调试";

    [Category(FeatureToggle), DisplayName("防止待机"), Description("启用后在空闲时定期按下 B，防止主机进入待机。")]
    public bool AntiIdle { get; set; }

    [Category(Operation), DisplayName("文件夹设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FolderSettings Folder { get; set; } = new();

    [Category(Operation), DisplayName("合法性设置")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public LegalitySettings Legality { get; set; } = new();

    [Category(FeatureToggle), DisplayName("启用文本日志"), Description("启用/禁用文本日志（需重启生效）。")]
    public bool LoggingEnabled { get; set; } = true;

    [Category(FeatureToggle), DisplayName("日志保留数量"), Description("保留的旧日志文件数量，设为 <=0 可禁用日志清理（需重启生效）。")]
    public int MaxArchiveFiles { get; set; } = 14;

    public abstract bool Shuffled { get; }

    [Browsable(false)]
    [Category(Debug), DisplayName("跳过创建机器人"), Description("启动程序时跳过创建机器人，便于测试集成。")]
    public bool SkipConsoleBotCreation { get; set; }
}
