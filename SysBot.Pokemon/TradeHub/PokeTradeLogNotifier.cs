using PKHeX.Core;
using SysBot.Base;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon;

public class PokeTradeLogNotifier<T> : IPokeTradeNotifier<T> where T : PKM, new()
{
    private int BatchTradeNumber { get; set; } = 1;
    private int TotalBatchTrades { get; set; } = 1;

    public Action<PokeRoutineExecutor<T>>? OnFinish { get; set; }

    public Task SendInitialQueueUpdate()
    {
        return Task.CompletedTask;
    }

    public void UpdateBatchProgress(int currentBatchNumber, T currentPokemon, int uniqueTradeID)
    {
        BatchTradeNumber = currentBatchNumber;
        // We can optionally log this update
        if (TotalBatchTrades > 1)
        {
            LogUtil.LogInfo("BatchTracker", $"批量交易进度：{currentBatchNumber}/{TotalBatchTrades} - {GameInfo.GetStrings("en").Species[currentPokemon.Species]}");
        }
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, string message)
    {
        // Add batch context if applicable
        if (info.TotalBatchTrades > 1)
        {
            TotalBatchTrades = info.TotalBatchTrades;
            message = $"[交易 {BatchTradeNumber}/{TotalBatchTrades}] {message}";
        }
        LogUtil.LogInfo(routine.Connection.Label, message);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeSummary message)
    {
        var msg = message.Summary;
        if (message.Details.Count > 0)
            msg += ", " + string.Join(", ", message.Details.Select(z => $"{z.Heading}: {z.Detail}"));

        // Add batch context if applicable
        if (info.TotalBatchTrades > 1)
        {
            TotalBatchTrades = info.TotalBatchTrades;
            msg = $"[交易 {BatchTradeNumber}/{TotalBatchTrades}] {msg}";
        }

        LogUtil.LogInfo(routine.Connection.Label, msg);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result, string message)
    {
        var batchInfo = info.TotalBatchTrades > 1 ? $"[交易 {BatchTradeNumber}/{info.TotalBatchTrades}] " : "";
        LogUtil.LogInfo(routine.Connection.Label, $"{batchInfo}通知 {info.Trainer.TrainerName} 关于 {GameInfo.GetStrings("en").Species[result.Species]} 的信息");
        LogUtil.LogInfo(routine.Connection.Label, $"{batchInfo}{message}");
    }

    public void TradeCanceled(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeResult msg)
    {
        var batchInfo = info.TotalBatchTrades > 1 ? $"[批量交易 {BatchTradeNumber}/{info.TotalBatchTrades}] " : "";
        LogUtil.LogInfo(routine.Connection.Label, $"{batchInfo}正在取消与 {info.Trainer.TrainerName} 的交易，原因：{msg}。");
        OnFinish?.Invoke(routine);
    }

    public void TradeFinished(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result)
    {
        // Print the nickname for Ledy trades so we can see what was requested.
        var ledyname = string.Empty;
        if ((info.Trainer.TrainerName == "Random Distribution" || info.Trainer.TrainerName == "随机派发") && result.IsNicknamed)
            ledyname = $" ({result.Nickname})";

        var batchInfo = info.TotalBatchTrades > 1 ? $"[交易 {BatchTradeNumber}/{info.TotalBatchTrades}] " : "";
        LogUtil.LogInfo(routine.Connection.Label, $"{batchInfo}已完成与 {info.Trainer.TrainerName} 的交易：发送 {GameInfo.GetStrings("en").Species[info.TradeData.Species]}，收到 {GameInfo.GetStrings("en").Species[result.Species]}{ledyname}");

        // Only invoke OnFinish for single trades or the last trade in a batch
        if (info.TotalBatchTrades <= 1 || BatchTradeNumber == info.TotalBatchTrades)
        {
            OnFinish?.Invoke(routine);
        }
    }

    public void TradeInitialize(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        var batchInfo = info.TotalBatchTrades > 1 ? $"[批量交易开始 - 共 {info.TotalBatchTrades} 笔] " : "";
        LogUtil.LogInfo(routine.Connection.Label, $"{batchInfo}开始为 {info.Trainer.TrainerName} 执行交易循环，发送 {GameInfo.GetStrings("en").Species[info.TradeData.Species]}");
    }

    public void TradeSearching(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        var batchInfo = info.TotalBatchTrades > 1 ? $"[交易 {BatchTradeNumber}/{info.TotalBatchTrades}] " : "";
        LogUtil.LogInfo(routine.Connection.Label, $"{batchInfo}正在寻找 {info.Trainer.TrainerName} 的连线对象，发送 {GameInfo.GetStrings("en").Species[info.TradeData.Species]}");
    }
}
