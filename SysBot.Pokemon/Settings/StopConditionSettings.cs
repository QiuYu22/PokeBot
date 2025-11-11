using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class StopConditionSettings
{
    private const string StopConditions = nameof(StopConditions);

    [Category(StopConditions), Description("在 EncounterBot 或 Fossilbot 找到匹配宝可梦时，按住 Capture 键录制 30 秒回放。")]
    public bool CaptureVideoClip { get; set; }

    [Category(StopConditions), Description("EncounterBot 或 Fossilbot 匹配后，按下 Capture 键前额外等待的毫秒数。")]
    public int ExtraTimeWaitCaptureVideo { get; set; } = 10000;

    [Category(StopConditions), Description("仅在拥有标记的宝可梦出现时停止。")]
    public bool MarkOnly { get; set; }

    [Category(StopConditions), Description("若不为空，将把此字符串追加到匹配结果的日志中，可用于 Echo 提醒指定对象。Discord 可使用 <@用户ID> 进行提醒。")]
    public string MatchFoundEchoMention { get; set; } = string.Empty;

    [Category(StopConditions), Description("设为 TRUE 时同时匹配 ShinyTarget 与 TargetIVs；否则满足任一条件即可。")]
    public bool MatchShinyAndIV { get; set; } = true;

    [Category(StopConditions), Description("选择要停止的闪光类型。")]
    public TargetShinyType ShinyTarget { get; set; } = TargetShinyType.DisableOption;

    [Category(StopConditions), Description("仅在匹配此 FormID 的宝可梦出现时停止。留空则不限制。")]
    public int? StopOnForm { get; set; }

    [Category(StopConditions), Description("仅在匹配此种族的宝可梦出现时停止。设为 \"None\" 则不限制。")]
    public Species StopOnSpecies { get; set; }

    [Category(StopConditions), Description("可接受的最大个体值，格式为 HP/Atk/Def/SpA/SpD/Spe。使用 \"x\" 忽略某项，\"/\" 为分隔符。")]
    public string TargetMaxIVs { get; set; } = "";

    [Category(StopConditions), Description("可接受的最小个体值，格式与最大个体值相同。")]
    public string TargetMinIVs { get; set; } = "";

    [Category(StopConditions), Description("仅在匹配此性格的宝可梦出现时停止。")]
    public Nature TargetNature { get; set; } = Nature.Random;

    [Category(StopConditions), Description("逗号分隔的不关注标记列表。请使用完整名称，例如 \"Uncommon Mark, Dawn Mark, Prideful Mark\"。")]
    public string UnwantedMarks { get; set; } = "";

    public static bool EncounterFound<T>(T pk, int[] targetminIVs, int[] targetmaxIVs, StopConditionSettings settings, IReadOnlyList<string>? marklist) where T : PKM
    {
        // Match Nature and Species if they were specified.
        if (settings.StopOnSpecies != Species.None && settings.StopOnSpecies != (Species)pk.Species)
            return false;

        if (settings.StopOnForm.HasValue && settings.StopOnForm != pk.Form)
            return false;

        if (settings.TargetNature != Nature.Random && settings.TargetNature != (Nature)pk.Nature)
            return false;

        // Return if it doesn't have a mark, or it has an unwanted mark.
        var unmarked = pk is IRibbonIndex m && !HasMark(m);
        var unwanted = marklist is not null && pk is IRibbonIndex m2 && settings.IsUnwantedMark(GetMarkName(m2), marklist);
        if (settings.MarkOnly && (unmarked || unwanted))
            return false;

        if (settings.ShinyTarget != TargetShinyType.DisableOption)
        {
            bool shinymatch = settings.ShinyTarget switch
            {
                TargetShinyType.AnyShiny => pk.IsShiny,
                TargetShinyType.NonShiny => !pk.IsShiny,
                TargetShinyType.StarOnly => pk.IsShiny && pk.ShinyXor != 0,
                TargetShinyType.SquareOnly => pk.ShinyXor == 0,
                TargetShinyType.DisableOption => true,
                _ => throw new ArgumentException(nameof(TargetShinyType)),
            };

            // If we only needed to match one of the criteria and it shiny match'd, return true.
            // If we needed to match both criteria, and it didn't shiny match, return false.
            if (!settings.MatchShinyAndIV && shinymatch)
                return true;
            if (settings.MatchShinyAndIV && !shinymatch)
                return false;
        }

        // Reorder the speed to be last.
        Span<int> pkIVList = stackalloc int[6];
        pk.GetIVs(pkIVList);
        (pkIVList[5], pkIVList[3], pkIVList[4]) = (pkIVList[3], pkIVList[4], pkIVList[5]);

        for (int i = 0; i < 6; i++)
        {
            if (targetminIVs[i] > pkIVList[i] || targetmaxIVs[i] < pkIVList[i])
                return false;
        }
        return true;
    }

    public static string GetMarkName(IRibbonIndex pk)
    {
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
                return GameInfo.Strings.Ribbons.GetName($"Ribbon{mark}");
        }
        return "";
    }

    public static string GetPrintName(PKM pk)
    {
        var set = ShowdownParsing.GetShowdownText(pk);
        if (pk is IRibbonIndex r)
        {
            var rstring = GetMarkName(r);
            if (!string.IsNullOrEmpty(rstring))
                set += $"\n检测到该宝可梦拥有 **{GetMarkName(r)}** 标记！";
        }
        return set;
    }

    public static void InitializeTargetIVs(PokeTradeHubConfig config, out int[] min, out int[] max)
    {
        min = ReadTargetIVs(config.StopConditions, true);
        max = ReadTargetIVs(config.StopConditions, false);
    }

    public static void ReadUnwantedMarks(StopConditionSettings settings, out IReadOnlyList<string> marks) =>
        marks = settings.UnwantedMarks.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

    public virtual bool IsUnwantedMark(string mark, IReadOnlyList<string> marklist) => marklist.Contains(mark);

    public override string ToString() => "停止条件设置";

    private static bool HasMark(IRibbonIndex pk)
    {
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
                return true;
        }
        return false;
    }

    private static int[] ReadTargetIVs(StopConditionSettings settings, bool min)
    {
        int[] targetIVs = new int[6];
        char[] split = ['/'];

        string[] splitIVs = min
            ? settings.TargetMinIVs.Split(split, StringSplitOptions.RemoveEmptyEntries)
            : settings.TargetMaxIVs.Split(split, StringSplitOptions.RemoveEmptyEntries);

        // Only accept up to 6 values.  Fill it in with default values if they don't provide 6.
        // Anything that isn't an integer will be a wild card.
        for (int i = 0; i < 6; i++)
        {
            if (i < splitIVs.Length)
            {
                var str = splitIVs[i];
                if (int.TryParse(str, out var val))
                {
                    targetIVs[i] = val;
                    continue;
                }
            }
            targetIVs[i] = min ? 0 : 31;
        }
        return targetIVs;
    }
}

public enum TargetShinyType
{
    DisableOption,  // Doesn't care

    NonShiny,       // Match nonshiny only

    AnyShiny,       // Match any shiny regardless of type

    StarOnly,       // Match star shiny only

    SquareOnly,     // Match square shiny only
}
