namespace SysBot.Base;

/// <summary>
/// 日志系统的配置项。
/// </summary>
public static class LogConfig
{
    /// <summary>
    /// 是否启用所有日志记录。
    /// </summary>
    public static bool LoggingEnabled { get; set; } = true;

    /// <summary>
    /// 保留的归档日志文件数量（默认：14 天）。
    /// </summary>
    public static int MaxArchiveFiles { get; set; } = 14;

    /// <summary>
    /// 是否在主日志之外生成每个机器人的独立日志文件。
    /// 启用后，每个机器人会写入 logs/{BotName}/SysBotLog.txt。
    /// 默认值：true，按连接创建独立文件夹存放日志。
    /// </summary>
    public static bool EnablePerBotLogging { get; set; } = true;

    /// <summary>
    /// 启用时，主日志文件仍会包含所有机器人的日志。
    /// 关闭时，仅写入各机器人独立日志（节省空间并提升性能）。
    /// 默认值：true，主日志保留全部内容以保持兼容性。
    /// </summary>
    public static bool EnableMasterLog { get; set; } = true;

    /// <summary>
    /// 是否在日志文件名中包含时间戳（便于排查特定会话）。
    /// 格式：logs/{BotName}/SysBotLog_{yyyy-MM-dd_HH-mm-ss}.txt。
    /// </summary>
    public static bool IncludeTimestampInFilename { get; set; } = false;

    /// <summary>
    /// 单个日志文件在归档前允许的最大大小（字节）。
    /// 默认：50MB（52428800 字节）。
    /// </summary>
    public static long MaxLogFileSize { get; set; } = 52428800;

    /// <summary>
    /// 系统组件是否统一记录到 “System” 目录，而非各自独立目录。
    /// 适用于运行大量系统服务时减少目录数量。
    /// 默认：true，将所有系统日志集中存放。
    /// </summary>
    public static bool ConsolidateSystemLogs { get; set; } = true;

    /// <summary>
    /// 被视为“系统组件”的身份前缀列表。
    /// 若启用 ConsolidateSystemLogs，这些身份的日志将写入 “System” 目录。
    /// </summary>
    public static readonly string[] SystemIdentities =
    [
        // Core system components
        "System", "SysBot", "Bot", "Form", "Hub",

        // Recovery and monitoring
        "Recovery", "RecoveryNotification",

        // Web services
        "WebServer", "WebTrade", "WebDump", "WebTradeNotifier", "TCP",

        // Discord integration
        "Discord", "SysCord",

        // Queue and trade management
        "QueueHelper", "TradeQueueInfo", "BatchTracker", "Barrier",

        // Utilities
        "Echo", "Dump", "Tray", "Legalizer",

        // Services
        "BotTaskService", "RestartManager", "PokemonPool"
    ];
}
