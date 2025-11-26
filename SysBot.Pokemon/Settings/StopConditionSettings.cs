using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class StopConditionSettings
{
    private const string StopConditions = "停止条件";

    [Category(StopConditions), Description("当遭遇机器人或化石机器人找到匹配的宝可梦时，按住截取按钮录制 30 秒视频。"), DisplayName("录制视频")]
    public bool CaptureVideoClip { get; set; }

    [Category(StopConditions), Description("遭遇匹配后按截取按钮前等待的额外时间（毫秒），用于遭遇机器人或化石机器人。"), DisplayName("录制视频等待时间")]
    public int ExtraTimeWaitCaptureVideo { get; set; } = 10000;

    [Category(StopConditions), Description("仅在有标记的宝可梦上停止。"), DisplayName("仅限标记")]
    public bool MarkOnly { get; set; }

    [Category(StopConditions), Description("如果不为空，提供的字符串将添加到找到结果的日志消息前，以向您指定的人发送回显警报。对于 Discord，使用 <@用户ID数字> 来提及。"), DisplayName("匹配发现提及")]
    public string MatchFoundEchoMention { get; set; } = string.Empty;

    [Category(StopConditions), Description("设置为 TRUE 时，同时匹配闪光目标和目标个体值设置。否则，查找闪光目标或目标个体值匹配。"), DisplayName("同时匹配闪光和 IV")]
    public bool MatchShinyAndIV { get; set; } = true;

    [Category(StopConditions), Description("选择要停止的闪光类型。"), DisplayName("闪光目标")]
    public TargetShinyType ShinyTarget { get; set; } = TargetShinyType.DisableOption;

    [Category(StopConditions), Description("仅在具有此形态 ID 的宝可梦上停止。留空则无限制。"), DisplayName("停止于形态")]
    public int? StopOnForm { get; set; }

    [Category(StopConditions), Description("仅在此物种的宝可梦上停止。设置为 \"None\" 则无限制。"), DisplayName("停止于物种")]
    public Species StopOnSpecies { get; set; }

    [Category(StopConditions), Description("最大可接受的个体值，格式为 HP/Atk/Def/SpA/SpD/Spe。使用 \"x\" 表示不检查的个体值，使用 \"/\" 作为分隔符。"), DisplayName("目标最大 IV")]
    public string TargetMaxIVs { get; set; } = "";

    [Category(StopConditions), Description("最小可接受的个体值，格式为 HP/Atk/Def/SpA/SpD/Spe。使用 \"x\" 表示不检查的个体值，使用 \"/\" 作为分隔符。"), DisplayName("目标最小 IV")]
    public string TargetMinIVs { get; set; } = "";

    [Category(StopConditions), Description("仅在指定性格的宝可梦上停止。"), DisplayName("目标性格")]
    public Nature TargetNature { get; set; } = Nature.Random;

    [Category(StopConditions), Description("要忽略的标记列表，用逗号分隔。使用完整名称，例如 \"Uncommon Mark, Dawn Mark, Prideful Mark\"。"), DisplayName("不需要的标记")]
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
                set += $"\n发现宝可梦拥有 **{GetMarkName(r)}**！";
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
