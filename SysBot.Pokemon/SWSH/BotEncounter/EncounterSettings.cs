using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class EncounterSettings : IBotStateSettings, ICountSettings
{
    private const string Counts = "统计";

    private const string Encounter = "遭遇";

    private const string Settings = "配置";

    private int _completedEggs;

    private int _completedFossils;

    private int _completedLegend;

    private int _completedWild;

    [Category(Counts), DisplayName("获取的蛋数量"), Description("已经获取（并统计）的蛋数量。")]
    public int CompletedEggs
    {
        get => _completedEggs;
        set => _completedEggs = value;
    }

    [Category(Counts), DisplayName("野外遭遇次数"), Description("已经遭遇的野外宝可梦数量。")]
    public int CompletedEncounters
    {
        get => _completedWild;
        set => _completedWild = value;
    }

    [Category(Counts), DisplayName("复活的化石宝可梦"), Description("已经复苏的化石宝可梦数量。")]
    public int CompletedFossils
    {
        get => _completedFossils;
        set => _completedFossils = value;
    }

    [Category(Counts), DisplayName("传说遭遇次数"), Description("已经遭遇的传说/幻之宝可梦数量。")]
    public int CompletedLegends
    {
        get => _completedLegend;
        set => _completedLegend = value;
    }

    [Category(Encounter), DisplayName("找到目标后的行为"), Description("找到符合条件的目标后，机器人应如何继续：停止、等待或继续执行。")]
    public ContinueAfterMatch ContinueAfterMatch { get; set; } = ContinueAfterMatch.StopExit;

    [Category(Counts), DisplayName("状态查询时回显计数"), Description("启用后，当外部请求状态信息时，会回显当前计数统计。")]
    public bool EmitCountsOnStatusCheck { get; set; }

    [Category(Encounter), DisplayName("遭遇方式"), Description("线路 / 重置 机器人所使用的遭遇方式。")]
    public EncounterMode EncounteringType { get; set; } = EncounterMode.VerticalLine;

    [Category(Settings), DisplayName("化石设置"), Description("用于化石复苏流程的额外参数。")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FossilSettings Fossil { get; set; } = new();

    [Category(Encounter), DisplayName("循环期间关闭屏幕"), Description("启用后，在正常循环运行时会关闭屏幕以节省电力。")]
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
            yield return $"野外遭遇次数：{CompletedEncounters}";
        if (CompletedLegends != 0)
            yield return $"传说遭遇次数：{CompletedLegends}";
        if (CompletedEggs != 0)
            yield return $"获取的蛋数量：{CompletedEggs}";
        if (CompletedFossils != 0)
            yield return $"完成的化石复苏数：{CompletedFossils}";
    }

    public override string ToString() => "剑盾遭遇机器人设置";
}
