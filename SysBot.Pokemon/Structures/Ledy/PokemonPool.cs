using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.IO;

namespace SysBot.Pokemon;

public class PokemonPool<T>(BaseConfig Settings) : List<T>
    where T : PKM, new()
{
    public readonly Dictionary<string, LedyRequest<T>> Files = [];

    private readonly int ExpectedSize = new T().Data.Length;

    private int Counter;

    private bool Randomized => Settings.Shuffled;

    public static bool DisallowRandomRecipientTrade(T pk)
    {
        // 惊喜交换禁止幻兽和传说，但允许次级传说。
        if (SpeciesCategory.IsLegendary(pk.Species))
            return true;
        if (SpeciesCategory.IsMythical(pk.Species))
            return true;

        // 融合形态无法进行惊喜交换。
        if (FormInfo.IsFusedForm(pk.Species, pk.Form, pk.Format))
            return true;

        return false;
    }

    public static void Shuffle(IList<T> items, int start, int end, Random rnd)
    {
        for (int i = start; i < end; i++)
        {
            int index = i + rnd.Next(end - i);
            (items[index], items[i]) = (items[i], items[index]);
        }
    }

    public T GetRandomPoke()
    {
        var choice = this[Counter];
        Counter = (Counter + 1) % Count;
        if (Counter == 0 && Randomized)
            Shuffle(this, 0, Count, Util.Rand);
        return choice;
    }

    public T GetRandomSurprise()
    {
        while (true)
        {
            var rand = GetRandomPoke();
            if (DisallowRandomRecipientTrade(rand))
                continue;
            return rand;
        }
    }

    public bool LoadFolder(string path, SearchOption opt = SearchOption.AllDirectories)
    {
        if (!Directory.Exists(path))
            return false;

        var loadedAny = false;
        var files = Directory.EnumerateFiles(path, "*", opt);
        var matchFiles = LoadUtil.GetFilesOfSize(files, ExpectedSize);

        const int surpriseBlocked = 0;
        foreach (var file in matchFiles)
        {
            var data = File.ReadAllBytes(file);
            var prefer = EntityFileExtension.GetContextFromExtension(file);
            var pkm = EntityFormat.GetFromBytes(data, prefer);
            if (pkm is null)
                continue;
            if (pkm is not T)
                pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _);
            if (pkm is not T dest)
                continue;

            if (dest.Species == 0)
            {
            LogUtil.LogInfo(nameof(PokemonPool<T>), "跳过：提供的文件无效：" + dest.FileName);
                continue;
            }

            if (!dest.CanBeTraded())
            {
            LogUtil.LogInfo(nameof(PokemonPool<T>), "跳过：提供的文件无法交易：" + dest.FileName);
                continue;
            }

            var la = new LegalityAnalysis(dest);
            if (!la.Valid)
            {
                var reason = la.Report();
                LogUtil.LogInfo(nameof(PokemonPool<T>), $"跳过：文件不合法：{dest.FileName} -- {reason}");
                continue;
            }

            if (Settings.Legality.ResetHOMETracker && dest is IHomeTrack h)
                h.Tracker = 0;

            var fn = Path.GetFileNameWithoutExtension(file);
            fn = StringsUtil.Sanitize(fn);

            // Since file names can be sanitized to the same string, only add one of them.
            if (!Files.ContainsKey(fn))
            {
                Add(dest);
                Files.Add(fn, new LedyRequest<T>(dest, fn));
            }
            else
            {
                LogUtil.LogInfo(nameof(PokemonPool<T>), "未添加：文件名重复：" + dest.FileName);
            }
            loadedAny = true;
        }
        if (surpriseBlocked == Count)
            LogUtil.LogInfo(nameof(PokemonPool<T>), "惊喜交换失败：未加载到任何兼容文件。");

        return loadedAny;
    }

    public bool Reload(string path, SearchOption opt = SearchOption.AllDirectories)
    {
        if (!Directory.Exists(path))
            return false;
        Clear();
        Files.Clear();
        return LoadFolder(path, opt);
    }
}
