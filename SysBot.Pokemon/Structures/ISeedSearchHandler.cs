using PKHeX.Core;

namespace SysBot.Pokemon;

public interface ISeedSearchHandler<T> where T : PKM, new()
{
    void CalculateAndNotify(T pkm, PokeTradeDetail<T> detail, SeedCheckSettings settings, PokeRoutineExecutor<T> bot);
}

public class NoSeedSearchHandler<T> : ISeedSearchHandler<T> where T : PKM, new()
{
    public void CalculateAndNotify(T pkm, PokeTradeDetail<T> detail, SeedCheckSettings settings, PokeRoutineExecutor<T> bot)
    {
        const string msg = "未找到种子搜索功能实现。请联系机器人服主，提供所需的 Z3 文件。";
        detail.SendNotification(bot, msg);
    }
}
