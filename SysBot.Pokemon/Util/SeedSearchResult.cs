using System;
using System.Collections.Generic;

namespace SysBot.Pokemon;

public class SeedSearchResult(Z3SearchResult Type, ulong Seed, int FlawlessIVCount, SeedCheckResults Mode)
{
    public static readonly SeedSearchResult None = new(Z3SearchResult.SeedNone, default, 0, SeedCheckResults.ClosestOnly);

    public readonly int FlawlessIVCount = FlawlessIVCount;

    public readonly SeedCheckResults Mode = Mode;

    public readonly ulong Seed = Seed;

    public readonly Z3SearchResult Type = Type;

    public override string ToString()
    {
        return Type switch
        {
            Z3SearchResult.SeedMismatch => $"已找到种子，但与目标不完全一致 {Seed:X16}",
            Z3SearchResult.Success => string.Join(Environment.NewLine, GetLines()),
            _ => "该宝可梦并非团战宝可梦！",
        };
    }

    private IEnumerable<string> GetLines()
    {
        if (FlawlessIVCount >= 1)
            yield return $"完美 IV 数：{FlawlessIVCount}";
        yield return "以下结果按完美 IV 数量列出。";

        SeedSearchUtil.GetShinyFrames(Seed, out int[] frames, out uint[] type, out List<uint[,]> IVs, Mode);

        for (int i = 0; i < 3 && frames[i] != 0; i++)
        {
            var shinytype = type[i] == 1 ? "星型" : "方块";
            yield return $"\n帧：{frames[i]} - {shinytype}";

            for (int ivcount = 0; ivcount < 5; ivcount++)
            {
                var ivlist = $"{ivcount + 1} - ";
                for (int j = 0; j < 6; j++)
                {
                    ivlist += IVs[i][ivcount, j];
                    if (j < 5)
                        ivlist += "/";
                }
                yield return $"{ivlist}";
            }
        }
    }
}
