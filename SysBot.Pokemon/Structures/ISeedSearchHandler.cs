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
        const string msg = "未找到种子搜索实现。请通知机器人托管者提供所需的 Z3 文件。";
        detail.SendNotification(bot, msg);
    }
}
