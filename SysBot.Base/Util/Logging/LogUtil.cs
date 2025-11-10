using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SysBot.Base;

/// <summary>
/// 日志封装工具（基于 NLog）。
/// 同时支持主日志（所有机器人）与单独的机器人日志文件，方便整理。
/// </summary>
public static class LogUtil
{
    // hook in here if you want to forward the message elsewhere
    public static readonly List<ILogForwarder> Forwarders = [];

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // 缓存各机器人记录器以避免重复创建
    private static readonly ConcurrentDictionary<string, Logger> BotLoggers = new();

    // 机器人识别前的日志缓冲
    // 键：连接 IP/USB 标识；值：缓冲的日志条目集合
    private static readonly ConcurrentDictionary<string, List<BufferedLogEntry>> LogBuffer = new();

    private static readonly string WorkingDirectory = Path.GetDirectoryName(Environment.ProcessPath)!;

    private record BufferedLogEntry(LogLevel Level, string Message, DateTime Timestamp);

    static LogUtil()
    {
        if (!LogConfig.LoggingEnabled)
            return;

        var config = new LoggingConfiguration();
        Directory.CreateDirectory("logs");

        // 主日志文件（汇总所有机器人），仅在启用时生成
        if (LogConfig.EnableMasterLog)
        {
            var masterLogFile = new FileTarget("masterlog")
            {
                FileName = Path.Combine(WorkingDirectory, "logs", "SysBotLog.txt"),
                ConcurrentWrites = true,

                ArchiveEvery = FileArchivePeriod.Day,
                ArchiveNumbering = ArchiveNumberingMode.Date,
                ArchiveFileName = Path.Combine(WorkingDirectory, "logs", "SysBotLog.{#}.txt"),
                ArchiveDateFormat = "yyyy-MM-dd",
                ArchiveAboveSize = LogConfig.MaxLogFileSize,
                MaxArchiveFiles = LogConfig.MaxArchiveFiles,
                Encoding = Encoding.Unicode,
                WriteBom = true,
            };
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, masterLogFile);
        }

        LogManager.Configuration = config;
    }

    public static DateTime LastLogged { get; private set; } = DateTime.Now;

    /// <summary>
    /// Gets or creates a per-bot logger for the specified bot identity
    /// </summary>
    /// <param name="identity">Bot identifier (e.g., "USB-1", "192.168.1.100")</param>
    /// <returns>Logger instance for the bot</returns>
    private static Logger GetOrCreateBotLogger(string identity)
    {
        if (!LogConfig.EnablePerBotLogging || !LogConfig.LoggingEnabled)
            return Logger;

        return BotLoggers.GetOrAdd(identity, botName =>
        {
            // Sanitize bot name for file system
            var safeBotName = SanitizeBotName(botName);
            var botLogDir = Path.Combine(WorkingDirectory, "logs", safeBotName);
            Directory.CreateDirectory(botLogDir);

            // 创建唯一的记录器名称以避免冲突
            var loggerName = $"BotLogger_{safeBotName}";
            var botLogger = LogManager.GetLogger(loggerName);

            // 配置每个机器人的日志目标
            var config = LogManager.Configuration ?? new LoggingConfiguration();

            var fileName = LogConfig.IncludeTimestampInFilename
                ? $"SysBotLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
                : "SysBotLog.txt";

            var botLogTarget = new FileTarget($"botlog_{safeBotName}")
            {
                FileName = Path.Combine(botLogDir, fileName),
                ConcurrentWrites = true,

                ArchiveEvery = FileArchivePeriod.Day,
                ArchiveNumbering = ArchiveNumberingMode.Date,
                ArchiveFileName = Path.Combine(botLogDir, "SysBotLog.{#}.txt"),
                ArchiveDateFormat = "yyyy-MM-dd",
                ArchiveAboveSize = LogConfig.MaxLogFileSize,
                MaxArchiveFiles = LogConfig.MaxArchiveFiles,
                Encoding = Encoding.Unicode,
                WriteBom = true,
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:inner=${newline}${exception:format=tostring}}"
            };

            config.AddTarget(botLogTarget);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, botLogTarget, loggerName);

            LogManager.Configuration = config;

            return botLogger;
        });
    }

    /// <summary>
    /// 将机器人名称转换为可用于文件路径的安全格式
    /// 例如生成目录：logs/HeXbyt3-483256/、logs/A-Z-734959/、logs/System/
    /// </summary>
    private static string SanitizeBotName(string botName)
    {
        if (string.IsNullOrWhiteSpace(botName))
            return "未知机器人";

        // 检查是否属于系统组件，需要合并到系统日志目录
        if (LogConfig.ConsolidateSystemLogs)
        {
            foreach (var systemIdentity in LogConfig.SystemIdentities)
            {
                if (botName.Equals(systemIdentity, StringComparison.OrdinalIgnoreCase) ||
                    botName.StartsWith(systemIdentity + " ", StringComparison.OrdinalIgnoreCase) ||
                    botName.StartsWith(systemIdentity + ":", StringComparison.OrdinalIgnoreCase))
                {
                    return "System";
                }
            }
        }

        // 保留完整的标识（例如 "HeXbyt3-483256"、"USB-1"），仅移除非法字符
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", botName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));

        // 去除首尾的空白符与下划线
        sanitized = sanitized.Trim('_', ' ');

        return string.IsNullOrWhiteSpace(sanitized) ? "未知机器人" : sanitized;
    }

    /// <summary>
    /// Checks if an identity is a trainer identifier (Name-XXXXXX format)
    /// </summary>
    private static bool IsTrainerIdentifier(string identity)
    {
        return identity.Contains('-') && System.Text.RegularExpressions.Regex.IsMatch(identity, @"-\d{6}$");
    }

    /// <summary>
    /// Checks if identity should skip per-bot logging (system-wide services)
    /// </summary>
    private static bool IsGlobalIdentity(string identity)
    {
        return LogConfig.SystemIdentities.Any(prefix => identity.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                                                         identity.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Flushes buffered logs from early identifier (IP/USB) to trainer folder
    /// </summary>
    public static void FlushBufferedLogs(string earlyIdentifier, string trainerIdentifier)
    {
        if (LogBuffer.TryRemove(earlyIdentifier, out var bufferedLogs))
        {
            var botLogger = GetOrCreateBotLogger(trainerIdentifier);
            foreach (var entry in bufferedLogs)
            {
                botLogger.Log(entry.Level, entry.Message);
            }
        }
    }

    public static void LogError(string identity, string message)
    {
        // Log to master log
        if (LogConfig.EnableMasterLog)
            Logger.Log(LogLevel.Error, $"{identity} {message}");

        // Handle per-bot logging
        if (LogConfig.EnablePerBotLogging && !IsGlobalIdentity(identity))
        {
            if (IsTrainerIdentifier(identity))
            {
                // Identified bot - log directly to trainer folder
                var botLogger = GetOrCreateBotLogger(identity);
                botLogger.Log(LogLevel.Error, message);
            }
            else
            {
                // Early bot identifier (IP/USB) - buffer for later
                LogBuffer.GetOrAdd(identity, _ => new List<BufferedLogEntry>())
                    .Add(new BufferedLogEntry(LogLevel.Error, message, DateTime.Now));
            }
        }

        // Forward to external listeners (Discord, etc.)
        foreach (var fwd in Forwarders)
        {
            try
            {
                fwd.Forward(message, identity);
            }
            catch { }
        }
    }

    public static void LogInfo(string identity, string message)
    {
        // Log to master log
        if (LogConfig.EnableMasterLog)
            Logger.Log(LogLevel.Info, $"{identity} {message}");

        // Handle per-bot logging
        if (LogConfig.EnablePerBotLogging && !IsGlobalIdentity(identity))
        {
            if (IsTrainerIdentifier(identity))
            {
                // Identified bot - log directly to trainer folder
                var botLogger = GetOrCreateBotLogger(identity);
                botLogger.Log(LogLevel.Info, message);
            }
            else
            {
                // Early bot identifier (IP/USB) - buffer for later
                LogBuffer.GetOrAdd(identity, _ => new List<BufferedLogEntry>())
                    .Add(new BufferedLogEntry(LogLevel.Info, message, DateTime.Now));
            }
        }

        // Forward to external listeners (Discord, etc.)
        foreach (var fwd in Forwarders)
        {
            try
            {
                fwd.Forward(message, identity);
            }
            catch { }
        }
    }

    public static void LogSuspicious(string identity, string message)
    {
        // Log to master log
        if (LogConfig.EnableMasterLog)
            Logger.Log(LogLevel.Warn, $"[安全] {identity} {message}");

        // Log to per-bot log
        if (LogConfig.EnablePerBotLogging)
        {
            var botLogger = GetOrCreateBotLogger(identity);
            botLogger.Log(LogLevel.Warn, $"[安全] {message}");
        }

        // Forward to external listeners (Discord, etc.)
        foreach (var fwd in Forwarders)
        {
            try
            {
                fwd.Forward($"[安全] {message}", identity);
            }
            catch { }
        }
    }

    public static void LogSafe(Exception exception, string identity)
    {
        // Log to master log
        if (LogConfig.EnableMasterLog)
        {
            Logger.Log(LogLevel.Error, $"来自 {identity} 的异常：");
            Logger.Log(LogLevel.Error, exception);
        }

        // Log to per-bot log
        if (LogConfig.EnablePerBotLogging)
        {
            var botLogger = GetOrCreateBotLogger(identity);
            botLogger.Log(LogLevel.Error, "发生异常：");
            botLogger.Log(LogLevel.Error, exception);
        }

        var err = exception.InnerException;
        while (err is not null)
        {
            if (LogConfig.EnableMasterLog)
                Logger.Log(LogLevel.Error, err);

            if (LogConfig.EnablePerBotLogging)
            {
                var botLogger = GetOrCreateBotLogger(identity);
                botLogger.Log(LogLevel.Error, err);
            }

            err = err.InnerException;
        }
    }

    public static void LogText(string message) => Logger.Log(LogLevel.Info, message);

    /// <summary>
    /// Clears the per-bot logger cache for a specific bot (useful when a bot disconnects)
    /// </summary>
    public static void ClearBotLogger(string identity)
    {
        BotLoggers.TryRemove(identity, out _);
    }

    /// <summary>
    /// 获取指定机器人的日志文件路径。
    /// </summary>
    public static string GetBotLogPath(string identity)
    {
        var safeBotName = SanitizeBotName(identity);
        var botLogDir = Path.Combine(WorkingDirectory, "logs", safeBotName);
        var fileName = LogConfig.IncludeTimestampInFilename
            ? $"SysBotLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
            : "SysBotLog.txt";
        return Path.Combine(botLogDir, fileName);
    }

    private static void Log(string message, string identity)
    {
        foreach (var fwd in Forwarders)
        {
            try
            {
                fwd.Forward(message, identity);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, $"转发来自 {identity} 的日志失败 - {message}");
                Logger.Log(LogLevel.Error, ex);
            }
        }

        LastLogged = DateTime.Now;
    }
}
