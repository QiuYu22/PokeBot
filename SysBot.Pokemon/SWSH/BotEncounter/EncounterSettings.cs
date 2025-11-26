using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class EncounterSettings : IBotStateSettings, ICountSettings
{
    private const string Counts = "统计";

    private const string Encounter = "遭遇";

    private const string Settings = "设置";

    private int _completedEggs;

    private int _completedFossils;

    private int _completedLegend;

    private int _completedWild;

    [Category(Counts), Description("获取的蛋数量"), DisplayName("已完成蛋")]
    public int CompletedEggs
    {
        get => _completedEggs;
        set => _completedEggs = value;
    }

    [Category(Counts), Description("遭遇的野生宝可梦"), DisplayName("已完成遭遇")]
    public int CompletedEncounters
    {
        get => _completedWild;
        set => _completedWild = value;
    }

    [Category(Counts), Description("复活的化石宝可梦"), DisplayName("已完成化石")]
    public int CompletedFossils
    {
        get => _completedFossils;
        set => _completedFossils = value;
    }

    [Category(Counts), Description("遭遇的传说宝可梦"), DisplayName("已完成传说")]
    public int CompletedLegends
    {
        get => _completedLegend;
        set => _completedLegend = value;
    }

    [Category(Encounter), Description("启用后，机器人在找到合适的匹配后将继续运行。"), DisplayName("匹配后继续")]
    public ContinueAfterMatch ContinueAfterMatch { get; set; } = ContinueAfterMatch.StopExit;

    [Category(Counts), Description("启用后，在请求状态检查时将输出统计数据。"), DisplayName("状态检查时输出统计")]
    public bool EmitCountsOnStatusCheck { get; set; }

    [Category(Encounter), Description("Line 和 Reset 机器人用于遭遇宝可梦的方法。"), DisplayName("遭遇类型")]
    public EncounterMode EncounteringType { get; set; } = EncounterMode.VerticalLine;

    [Category(Settings), DisplayName("化石")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FossilSettings Fossil { get; set; } = new();

    [Category(Encounter), Description("启用后，在正常机器人循环操作期间关闭屏幕以节省电量。"), DisplayName("关闭屏幕")]
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
            yield return $"野生遭遇: {CompletedEncounters}";
        if (CompletedLegends != 0)
            yield return $"传说遭遇: {CompletedLegends}";
        if (CompletedEggs != 0)
            yield return $"获得蛋: {CompletedEggs}";
        if (CompletedFossils != 0)
            yield return $"已完成化石: {CompletedFossils}";
    }

    public override string ToString() => "遭遇机器人 SWSH 设置";
}
