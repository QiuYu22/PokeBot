using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class StopConditionSettings
{
    private const string StopConditions = "停止条件";

    [Category(StopConditions), DisplayName("匹配时录制视频"), Description("EncounterBot/Fossilbot 找到匹配宝可梦时长按截图键录制 30 秒视频。")]
    public bool CaptureVideoClip { get; set; }

    [Category(StopConditions), DisplayName("录制前额外等待 (ms)"), Description("匹配后按下截图键前额外等待的毫秒数。")]
    public int ExtraTimeWaitCaptureVideo { get; set; } = 10000;

    [Category(StopConditions), DisplayName("仅包含有印记"), Description("仅当宝可梦拥有印记时才停止。")]
    public bool MarkOnly { get; set; }

    [Category(StopConditions), DisplayName("匹配提醒附加文本"), Description("在结果日志前追加的文本，用于提醒指定对象（Discord 可使用 <@userID>）。")]
    public string MatchFoundEchoMention { get; set; } = string.Empty;

    [Category(StopConditions), DisplayName("同时匹配闪光与个体"), Description("为 True 时需同时满足 ShinyTarget 与 IV 条件，否则任一满足即可。")]
    public bool MatchShinyAndIV { get; set; } = true;

    [Category(StopConditions), DisplayName("闪光匹配类型"), Description("选择需要停止的闪光类型。")]
    public TargetShinyType ShinyTarget { get; set; } = TargetShinyType.DisableOption;

    [Category(StopConditions), DisplayName("限定形态 ID"), Description("仅在匹配指定 FormID 时停止；为空表示不限。")]
    public int? StopOnForm { get; set; }

    [Category(StopConditions), DisplayName("限定物种"), Description("仅在匹配指定物种时停止；设置为 None 表示不限。")]
    public Species StopOnSpecies { get; set; }

    [Category(StopConditions), DisplayName("最大接受 IV"), Description("HP/Atk/Def/SpA/SpD/Spe 格式的最大 IV，使用“x”忽略，使用“/”分隔。")]
    public string TargetMaxIVs { get; set; } = "";

    [Category(StopConditions), DisplayName("最小接受 IV"), Description("HP/Atk/Def/SpA/SpD/Spe 格式的最小 IV，使用“x”忽略，使用“/”分隔。")]
    public string TargetMinIVs { get; set; } = "";

    [Category(StopConditions), DisplayName("限定性格"), Description("仅在符合指定性格时停止。")]
    public Nature TargetNature { get; set; } = Nature.Random;

    [Category(StopConditions), DisplayName("忽略的印记"), Description("以逗号分隔的印记名称列表，匹配这些印记时视为无效。")]
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
                set += $"\n发现宝可梦带有 **{GetMarkName(r)}**！";
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
