namespace SysBot.Base;

/// <summary>
/// Standardized log identity constants to ensure consistent folder structure.
/// Only use these constants for logging - no arbitrary strings!
/// </summary>
public static class LogIdentity
{
    /// <summary>
    /// System-level operations: startup, shutdown, recovery, configuration, etc.
    /// All system logs go to logs/System/
    /// 系统级操作：启动、关闭、恢复、配置等
    /// </summary>
    public const string System = "系统";

    /// <summary>
    /// For bot-specific operations, use the bot's Connection.Label (IP or USB identifier)
    /// Examples: "192.168.0.106", "USB-1"
    /// These create per-bot folders: logs/192.168.0.106/
    /// </summary>
    public static string Bot(string connectionLabel) => connectionLabel;
}
