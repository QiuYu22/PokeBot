using PKHeX.Core;

namespace SysBot.Pokemon;

/// <summary>
/// 包含游戏外玩家提交的游戏内交易数据请求详情。
/// </summary>
/// <typeparam name="T">与所接收游戏相匹配的格式。</typeparam>
public sealed record TradeEntry<T> where T : PKM, new()
{
    public readonly ulong UserID;
    public readonly string Username;
    public readonly PokeTradeDetail<T> Trade;
    public readonly PokeRoutineType Type;
    public readonly int UniqueTradeID;

    public TradeEntry(PokeTradeDetail<T> trade, ulong userID, PokeRoutineType type, string username, int uniqueTradeID)
    {
        Trade = trade;
        UserID = userID;
        Type = type;
        Username = username;
        UniqueTradeID = uniqueTradeID;
    }

    /// <summary>
    /// 检查提供的 <see cref="uid"/> 与 <see cref="uniqueTradeID"/> 是否与本对象匹配。
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="uniqueTradeID"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public bool Equals(ulong uid, int uniqueTradeID, PokeRoutineType type = 0)
    {
        if (UserID != uid || UniqueTradeID != uniqueTradeID)
            return false;

        return type == 0 || type == Type;
    }

    public override string ToString() => $"(ID {Trade.ID}) {Username} {UserID:D19} - {Type} - 唯一交易 ID：{UniqueTradeID}";
}
