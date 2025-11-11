using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Base;

/// <summary>
/// 监控机器人健康状态并在崩溃后自动尝试恢复的服务。
/// </summary>
public sealed class BotRecoveryService<T> : IDisposable where T : class, IConsoleBotConfig
{
    private readonly BotRunner<T> _runner;
    private readonly RecoveryConfiguration _config;
    private readonly ConcurrentDictionary<string, BotRecoveryState> _recoveryStates = new();
    private readonly PeriodicTimer _periodicTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _recoveryLock = new(1, 1);
    private readonly Task _monitorTask;
    private bool _isDisposed;

    public event EventHandler<BotRecoveryEventArgs>? RecoveryAttempted;
    public event EventHandler<BotRecoveryEventArgs>? RecoverySucceeded;
    public event EventHandler<BotRecoveryEventArgs>? RecoveryFailed;
    public event EventHandler<BotCrashEventArgs>? BotCrashed;

    public BotRecoveryService(BotRunner<T> runner, RecoveryConfiguration config)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        ValidateConfiguration(_config);
        
        // 使用 PeriodicTimer 以获得更好的异步支持
        _periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        _monitorTask = MonitorBotsAsync(_cancellationTokenSource.Token);
    }

    private static void ValidateConfiguration(RecoveryConfiguration config)
    {
        if (config.MaxRecoveryAttempts < 1)
            throw new ArgumentException("配置参数 '最大连续恢复次数' 必须至少为 1。", nameof(config));
        if (config.InitialRecoveryDelaySeconds < 0)
            throw new ArgumentException("配置参数 '首次恢复延迟（秒）' 不能为负数。", nameof(config));
        if (config.BackoffMultiplier < 1.0)
            throw new ArgumentException("配置参数 '延迟倍增系数' 必须至少为 1.0。", nameof(config));
    }

    /// <summary>
    /// 注册机器人以执行崩溃监控与自动恢复。
    /// </summary>
    public void RegisterBot(BotSource<T> bot)
    {
        var state = new BotRecoveryState
        {
            BotName = bot.Bot.Connection.Name,
            LastStartTime = DateTime.UtcNow,
            IsIntentionallyStopped = false
        };
        
        _recoveryStates.AddOrUpdate(bot.Bot.Connection.Name, state, (_, __) => state);
    }

    /// <summary>
    /// 将机器人标记为人工停止，以避免触发恢复。
    /// </summary>
    public void MarkIntentionallyStopped(string botName)
    {
        if (_recoveryStates.TryGetValue(botName, out var state))
        {
            state.IsIntentionallyStopped = true;
        }
    }

    /// <summary>
    /// 清除机器人的人工停止标记。
    /// </summary>
    public void ClearIntentionallyStopped(string botName)
    {
        if (_recoveryStates.TryGetValue(botName, out var state))
        {
            state.IsIntentionallyStopped = false;
        }
    }

    private async Task MonitorBotsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _periodicTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
                
                if (!_config.EnableRecovery || _isDisposed)
                    continue;

                await MonitorAndRecoverBots(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 当收到取消请求时属于预期行为
                break;
            }
            catch (Exception ex)
            {
                LogUtil.LogError("恢复", $"机器人恢复监视器出错：{ex.Message}");
            }
        }
    }

    private async Task MonitorAndRecoverBots(CancellationToken cancellationToken)
    {
        var botsToRecover = new List<(BotSource<T> bot, BotRecoveryState state)>();

        await _recoveryLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var bot in _runner.Bots)
            {
                var botName = bot.Bot.Connection.Name;
                if (!_recoveryStates.TryGetValue(botName, out var state))
                    continue;

                // 检查机器人是否已崩溃或停止
                if (!bot.IsRunning && !bot.IsStopping && !state.IsRecovering)
                {
                    // 判断是否应该尝试恢复
                    if (ShouldAttemptRecovery(bot, state))
                    {
                        botsToRecover.Add((bot, state));
                        state.IsRecovering = true;
                    }
                }
                else if (bot.IsRunning && state.ConsecutiveFailures > 0)
                {
                    // 机器人正在运行，检查稳定时间是否足以重置尝试次数
                    var uptime = DateTime.UtcNow - state.LastStartTime;
                    if (uptime.TotalSeconds >= _config.MinimumStableUptimeSeconds)
                    {
                        state.ConsecutiveFailures = 0;
                        LogUtil.LogInfo("恢复", $"机器人 {botName} 已稳定运行 {uptime.TotalMinutes:F1} 分钟，正在重置恢复尝试次数。");
                    }
                }
            }
        }
        finally
        {
            _recoveryLock.Release();
        }

        // 尝试恢复发生崩溃的机器人
        foreach (var (bot, state) in botsToRecover)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            await AttemptRecovery(bot, state, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool ShouldAttemptRecovery(BotSource<T> bot, BotRecoveryState state)
    {
        var botName = bot.Bot.Connection.Name;

        // 如果是人工停止且配置不允许恢复，则直接跳过
        if (state.IsIntentionallyStopped && !_config.RecoverIntentionalStops)
        {
            return false;
        }

        // 检查是否超过最大尝试次数
        if (state.ConsecutiveFailures >= _config.MaxRecoveryAttempts)
        {
            LogUtil.LogError("恢复", $"机器人 {botName} 的恢复尝试次数已达到上限（{_config.MaxRecoveryAttempts} 次）。");
            return false;
        }

        // 清理过期的崩溃记录
        state.RemoveOldCrashes(crash => 
            (DateTime.UtcNow - crash).TotalMinutes > _config.CrashHistoryWindowMinutes);

        // 检查崩溃频率是否超标
        if (state.CrashHistory.Count >= _config.MaxCrashesInWindow)
        {
            LogUtil.LogError("恢复", $"机器人 {botName} 在最近 {_config.CrashHistoryWindowMinutes} 分钟内崩溃 {state.CrashHistory.Count} 次，已停用自动恢复。");
            return false;
        }

        // 检查冷却时间是否已过
        if (state.LastRecoveryAttempt.HasValue)
        {
            var timeSinceLastAttempt = DateTime.UtcNow - state.LastRecoveryAttempt.Value;
            var requiredDelay = CalculateBackoffDelay(state.ConsecutiveFailures);
            
            if (timeSinceLastAttempt.TotalSeconds < requiredDelay)
            {
                return false;
            }
        }

        return true;
    }

    private double CalculateBackoffDelay(int attemptNumber)
    {
        var delay = _config.InitialRecoveryDelaySeconds * Math.Pow(_config.BackoffMultiplier, attemptNumber);
        return Math.Min(delay, _config.MaxRecoveryDelaySeconds);
    }

    private async Task AttemptRecovery(BotSource<T> bot, BotRecoveryState state, CancellationToken cancellationToken)
    {
        var botName = bot.Bot.Connection.Name;
        state.LastRecoveryAttempt = DateTime.UtcNow;
        state.ConsecutiveFailures++;
        state.AddCrashTime(DateTime.UtcNow);

        try
        {
            // 触发崩溃通知
            BotCrashed?.Invoke(this, new BotCrashEventArgs 
            { 
                BotName = botName, 
                CrashTime = DateTime.UtcNow,
                AttemptNumber = state.ConsecutiveFailures 
            });

            LogUtil.LogInfo("恢复", $"正在尝试恢复机器人 {botName}（第 {state.ConsecutiveFailures}/{_config.MaxRecoveryAttempts} 次）。");

            if (_config.NotifyOnRecoveryAttempt)
            {
                RecoveryAttempted?.Invoke(this, new BotRecoveryEventArgs 
                { 
                    BotName = botName, 
                    AttemptNumber = state.ConsecutiveFailures,
                    IsSuccess = false 
                });
            }

            // 等待退避延迟
            var delay = CalculateBackoffDelay(state.ConsecutiveFailures - 1);
            await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken).ConfigureAwait(false);

            // 尝试重新启动机器人
            await Task.Run(() =>
            {
                try
                {
                    bot.Start();
                    state.LastStartTime = DateTime.UtcNow;
                    state.IsIntentionallyStopped = false;
                }
                catch (Exception ex)
                {
                    LogUtil.LogError("恢复", $"启动机器人 {botName} 失败：{ex.Message}");
                    throw;
                }
            }).ConfigureAwait(false);

            // 等待片刻以确认机器人是否保持运行
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);

            if (bot.IsRunning)
            {
                LogUtil.LogInfo("恢复", $"机器人 {botName} 已成功恢复。");
                RecoverySucceeded?.Invoke(this, new BotRecoveryEventArgs 
                { 
                    BotName = botName, 
                    AttemptNumber = state.ConsecutiveFailures,
                    IsSuccess = true 
                });
            }
            else
            {
                throw new Exception("机器人在重启后立刻停止。");
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError("恢复", $"恢复机器人 {botName} 失败：{ex.Message}");
            
            if (state.ConsecutiveFailures >= _config.MaxRecoveryAttempts && _config.NotifyOnRecoveryFailure)
            {
                RecoveryFailed?.Invoke(this, new BotRecoveryEventArgs 
                { 
                    BotName = botName, 
                    AttemptNumber = state.ConsecutiveFailures,
                    IsSuccess = false,
                    FailureReason = ex.Message
                });
            }
        }
        finally
        {
            state.IsRecovering = false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        
        // 取消监控任务
        _cancellationTokenSource.Cancel();
        
        try
        {
            // 等待监控任务结束
            _monitorTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // 任务已取消，属于预期行为
        }
        
        _periodicTimer.Dispose();
        _cancellationTokenSource.Dispose();
        _recoveryLock.Dispose();
    }

    /// <summary>
    /// 获取指定机器人的当前恢复状态。
    /// </summary>
    public BotRecoveryState? GetRecoveryState(string botName)
    {
        return _recoveryStates.TryGetValue(botName, out var state) ? state : null;
    }

    /// <summary>
    /// 重置指定机器人的恢复状态。
    /// </summary>
    public void ResetRecoveryState(string botName)
    {
        if (_recoveryStates.TryGetValue(botName, out var state))
        {
            state.ConsecutiveFailures = 0;
            state.ClearCrashHistory();
            state.LastRecoveryAttempt = null;
            state.IsRecovering = false;
        }
    }
    
    /// <summary>
    /// 启用恢复服务。
    /// </summary>
    public void EnableRecovery()
    {
        _config.EnableRecovery = true;
        LogUtil.LogInfo("恢复", "机器人恢复服务已启用。");
    }
    
    /// <summary>
    /// 停用恢复服务。
    /// </summary>
    public void DisableRecovery()
    {
        _config.EnableRecovery = false;
        LogUtil.LogInfo("恢复", "机器人恢复服务已停用。");
    }
}

/// <summary>
/// 恢复服务的配置项（RecoverySettings 的精简版本）。
/// </summary>
public class RecoveryConfiguration
{
    public bool EnableRecovery { get; set; } = true;
    public int MaxRecoveryAttempts { get; set; } = 3;
    public int InitialRecoveryDelaySeconds { get; set; } = 5;
    public int MaxRecoveryDelaySeconds { get; set; } = 300;
    public double BackoffMultiplier { get; set; } = 2.0;
    public int CrashHistoryWindowMinutes { get; set; } = 60;
    public int MaxCrashesInWindow { get; set; } = 5;
    public bool RecoverIntentionalStops { get; set; } = false;
    public int MinimumStableUptimeSeconds { get; set; } = 600;
    public bool NotifyOnRecoveryAttempt { get; set; } = true;
    public bool NotifyOnRecoveryFailure { get; set; } = true;
}

/// <summary>
/// 跟踪单个机器人的恢复状态。
/// </summary>
public class BotRecoveryState
{
    private readonly ConcurrentBag<DateTime> _crashHistory = new();
    private int _consecutiveFailures;
    private bool _isRecovering;
    
    public string BotName { get; init; } = string.Empty;
    
    public int ConsecutiveFailures 
    { 
        get => _consecutiveFailures;
        set => Interlocked.Exchange(ref _consecutiveFailures, value);
    }
    
    public IReadOnlyCollection<DateTime> CrashHistory => _crashHistory;
    public DateTime? LastRecoveryAttempt { get; set; }
    public DateTime LastStartTime { get; set; }
    public bool IsIntentionallyStopped { get; set; }
    
    public bool IsRecovering 
    { 
        get => _isRecovering;
        set => _isRecovering = value;
    }
    
    public void AddCrashTime(DateTime crashTime)
    {
        _crashHistory.Add(crashTime);
    }
    
    public void ClearCrashHistory()
    {
        _crashHistory.Clear();
    }
    
    public int RemoveOldCrashes(Func<DateTime, bool> predicate)
    {
        var current = _crashHistory.ToList();
        var toKeep = current.Where(crash => !predicate(crash)).ToList();
        
        if (current.Count == toKeep.Count)
            return 0;
            
        _crashHistory.Clear();
        foreach (var crash in toKeep)
            _crashHistory.Add(crash);
            
        return current.Count - toKeep.Count;
    }
}

/// <summary>
/// 机器人恢复事件的参数。
/// </summary>
public class BotRecoveryEventArgs : EventArgs
{
    public string BotName { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public bool IsSuccess { get; set; }
    public string? FailureReason { get; set; }
}

/// <summary>
/// 机器人崩溃事件的参数。
/// </summary>
public class BotCrashEventArgs : EventArgs
{
    public string BotName { get; set; } = string.Empty;
    public DateTime CrashTime { get; set; }
    public int AttemptNumber { get; set; }
}