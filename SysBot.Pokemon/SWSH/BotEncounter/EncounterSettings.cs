using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class EncounterSettings : IBotStateSettings, ICountSettings
{
    private const string Counts = nameof(Counts);

    private const string Encounter = nameof(Encounter);

    private const string Settings = nameof(Settings);

    private int _completedEggs;

    private int _completedFossils;

    private int _completedLegend;

    private int _completedWild;

    [Category(Counts), Description("已获取的蛋数量")]
    public int CompletedEggs
    {
        get => _completedEggs;
        set => _completedEggs = value;
    }

    [Category(Counts), Description("已遭遇的野生宝可梦数量")]
    public int CompletedEncounters
    {
        get => _completedWild;
        set => _completedWild = value;
    }

    [Category(Counts), Description("已复活的化石宝可梦数量")]
    public int CompletedFossils
    {
        get => _completedFossils;
        set => _completedFossils = value;
    }

    [Category(Counts), Description("已遭遇的传说宝可梦数量")]
    public int CompletedLegends
    {
        get => _completedLegend;
        set => _completedLegend = value;
    }

    [Category(Encounter), Description("启用时，找到符合条件的宝可梦后继续运行。")]
    public ContinueAfterMatch ContinueAfterMatch { get; set; } = ContinueAfterMatch.StopExit;

    [Category(Counts), Description("启用时，在请求状态时输出统计数据。")]
    public bool EmitCountsOnStatusCheck { get; set; }

    [Category(Encounter), Description("Line/Reset 机器人用于遭遇宝可梦的方式。")]
    public EncounterMode EncounteringType { get; set; } = EncounterMode.VerticalLine;

    [Category(Settings)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FossilSettings Fossil { get; set; } = new();

    [Category(Encounter), Description("启用时，在常规循环期间关闭屏幕以节省电量。")]
    public bool ScreenOff { get; set; }

    public int AddCompletedEggs() => Interlocked.Increment(ref _completedEggs);

    public int AddCompletedEncounters() => Interlocked.Increment(ref _completedWild);

    public int AddCompletedFossils() => Interlocked.Increment(ref _completedFossils);

    public int AddCompletedLegends() => Interlocked.Increment(ref _completedLegend);

    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedEncounters != 0)
            yield return $"野生遭遇次数：{CompletedEncounters}";
        if (CompletedLegends != 0)
            yield return $"传说遭遇次数：{CompletedLegends}";
        if (CompletedEggs != 0)
            yield return $"获取的蛋数量：{CompletedEggs}";
        if (CompletedFossils != 0)
            yield return $"复活的化石数量：{CompletedFossils}";
    }

    public override string ToString() => "遭遇机器人 SWSH 设置";
}
