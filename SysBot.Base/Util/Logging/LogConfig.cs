namespace SysBot.Base;

/// <summary>
/// Configuration for the logging system
/// </summary>
public static class LogConfig
{
    /// <summary>
    /// Enable or disable all logging
    /// </summary>
    public static bool LoggingEnabled { get; set; } = true;

    /// <summary>
    /// Number of archived log files to keep (default: 14 days)
    /// </summary>
    public static int MaxArchiveFiles { get; set; } = 14;

    /// <summary>
    /// Enable per-bot log files in addition to the master log
    /// When enabled, each bot will have its own log file: logs/{BotName}/SysBotLog.txt
    /// DEFAULT: true - each IP gets its own folder with all related logs
    /// </summary>
    public static bool EnablePerBotLogging { get; set; } = true;

    /// <summary>
    /// When enabled, the master log file will still contain all bot logs
    /// When disabled, only per-bot logs are written (saves disk space and improves performance)
    /// DEFAULT: true - master log contains everything for backward compatibility
    /// </summary>
    public static bool EnableMasterLog { get; set; } = true;

    /// <summary>
    /// Include timestamp in log file names (useful for debugging specific sessions)
    /// Format: logs/{BotName}/SysBotLog_{yyyy-MM-dd_HH-mm-ss}.txt
    /// </summary>
    public static bool IncludeTimestampInFilename { get; set; } = false;

    /// <summary>
    /// Maximum size for individual log files before archiving (in bytes)
    /// Default: 50MB (52428800 bytes)
    /// </summary>
    public static long MaxLogFileSize { get; set; } = 52428800;

    /// <summary>
    /// System components will log to a single "System" folder instead of individual folders
    /// Reduces clutter when you have many system services running
    /// DEFAULT: true - consolidate all system logs into one folder
    /// </summary>
    public static bool ConsolidateSystemLogs { get; set; } = true;

    /// <summary>
    /// List of identity prefixes that are considered "system" components
    /// These will all log to the "System" folder if ConsolidateSystemLogs is enabled
    /// 被视为"系统"组件的标识前缀列表
    /// </summary>
    public static readonly string[] SystemIdentities =
    [
        // Core system components / 核心系统组件
        "System", "系统", "SysBot", "系统机器人", "Bot", "机器人", "Form", "窗体", "Hub", "中心",

        // Recovery and monitoring / 恢复和监控
        "Recovery", "恢复", "RecoveryNotification", "恢复通知",

        // Web services / Web 服务
        "WebServer", "Web服务器", "WebTrade", "Web交易", "WebDump", "Web导出", "WebTradeNotifier", "Web交易通知", "TCP",

        // Discord integration / Discord 集成
        "Discord", "SysCord",

        // Queue and trade management / 队列和交易管理
        "QueueHelper", "队列助手", "TradeQueueInfo", "交易队列信息", "BatchTracker", "批量追踪器", "Barrier", "同步屏障", "TradeCodeStorage", "交易码存储",

        // Utilities / 工具
        "Echo", "回显", "Dump", "导出", "Tray", "托盘", "Legalizer", "合法化管理", "AutoOT", "自动OT",

        // Services / 服务
        "BotTaskService", "机器人任务服务", "RestartManager", "重启管理器", "PokemonPool", "宝可梦数据库", "UpdateManager", "更新管理器"
    ];
}
