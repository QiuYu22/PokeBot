using PKHeX.Core;
using SysBot.Base;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon;

public class QueueMonitor<T>(PokeTradeHub<T> Hub)
    where T : PKM, new()
{
    // 队列状态变更时调用的动作：(是否满员, 当前数量, 最大数量)
    public static Action<bool, int, int>? OnQueueStatusChanged { get; set; }
    public async Task MonitorOpenQueue(CancellationToken token)
    {
        var queues = Hub.Queues.Info;
        var settings = Hub.Config.Queues;
        float secWaited = 0;

        const int sleepDelay = 0_500;
        const float sleepSeconds = sleepDelay / 1000f;
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(sleepDelay, token).ConfigureAwait(false);
            var mode = settings.QueueToggleMode;
            if (!UpdateCanQueue(mode, settings, queues, secWaited))
            {
                secWaited += sleepSeconds;
                continue;
            }

            // 队列设置已更新，广播状态变更。
            secWaited = 0;
            var state = queues.GetCanQueue()
                ? "交易队列已开启，用户可以加入。"
                : "队列设置已更改：**用户暂时无法加入队列，直到重新开启。**";
            EchoUtil.Echo(state);
        }
    }

    private static bool CheckInterval(QueueSettings settings, TradeQueueInfo<T> queues, float secWaited)
    {
        if (settings.CanQueue)
        {
            if (secWaited >= settings.IntervalOpenFor)
                queues.ToggleQueue();
            else
                return false;
        }
        else
        {
            if (secWaited >= settings.IntervalCloseFor)
                queues.ToggleQueue();
            else
                return false;
        }

        return true;
    }

    private bool CheckThreshold(QueueSettings settings, TradeQueueInfo<T> queues)
    {
        if (settings.CanQueue)
        {
            if (queues.Count >= settings.ThresholdLock)
            {
                queues.ToggleQueue();
                // 队列已满/已关闭
                if (settings.NotifyOnQueueClose)
                    OnQueueStatusChanged?.Invoke(true, queues.Count, settings.MaxQueueCount);
            }
            else
                return false;
        }
        else
        {
            if (queues.Count <= settings.ThresholdUnlock)
            {
                queues.ToggleQueue();
                // 队列已重新开放
                if (settings.NotifyOnQueueClose)
                    OnQueueStatusChanged?.Invoke(false, queues.Count, settings.MaxQueueCount);
            }
            else
                return false;
        }

        return true;
    }

    private bool UpdateCanQueue(QueueOpening mode, QueueSettings settings, TradeQueueInfo<T> queues, float secWaited)
    {
        return mode switch
        {
            QueueOpening.Threshold => CheckThreshold(settings, queues),
            QueueOpening.Interval => CheckInterval(settings, queues, secWaited),
            _ => false,
        };
    }
}
