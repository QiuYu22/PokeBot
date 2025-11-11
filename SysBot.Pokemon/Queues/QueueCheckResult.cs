using PKHeX.Core;

namespace SysBot.Pokemon;

/// <summary>
/// Stores data for indicating how a queue position/presence check resulted.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed record QueueCheckResult<T> where T : PKM, new()
{
    public readonly bool InQueue;
    public readonly TradeEntry<T>? Detail;
    public readonly int Position;
    public readonly int QueueCount;
    public readonly int BatchNumber;
    public readonly int TotalBatchTrades;

    public static readonly QueueCheckResult<T> None = new();

    public QueueCheckResult(bool inQueue = false, TradeEntry<T>? detail = default, int position = -1, int queueCount = -1, int batchNumber = 1, int totalBatchTrades = 1)
    {
        InQueue = inQueue;
        Detail = detail;
        Position = position;
        QueueCount = queueCount;
        BatchNumber = batchNumber;
        TotalBatchTrades = totalBatchTrades;
    }

    public string GetMessage()
    {
        if (!InQueue || Detail is null)
            return "你当前不在队列中。";

        var position = $"{Position + BatchNumber - 1}/{QueueCount}";
        var msg = $"你正在 {Detail.Type} 队列中！当前位置：{position}（ID {Detail.Trade.ID}）";

        var pk = Detail.Trade.TradeData;
        if (pk.Species != 0)
            msg += $"，预计获得：{GameInfo.GetStrings("zh-Hans").Species[pk.Species]}";

        if (TotalBatchTrades > 1)
            msg += $"（批量交易 {BatchNumber}/{TotalBatchTrades}）";

        return msg;
    }
}
