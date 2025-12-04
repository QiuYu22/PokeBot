using System.ComponentModel;

namespace SysBot.Pokemon;

public class SeedCheckSettings
{
    private const string FeatureToggle = "功能开关";

    [Category(FeatureToggle), DisplayName("结果显示模式"), Description("控制仅返回最近闪光帧、首个星形/方形闪光帧，或前三个闪光帧。")]
    public SeedCheckResults ResultDisplayMode { get; set; }

    [Category(FeatureToggle), DisplayName("显示全部 Z3 结果"), Description("启用后返回所有可能的种子结果，而非首个匹配。")]
    public bool ShowAllZ3Results { get; set; }

    public override string ToString() => "种子检测设置";
}

public enum SeedCheckResults
{
    ClosestOnly,            // Only gets the first shiny

    FirstStarAndSquare,     // Gets the first star shiny and first square shiny

    FirstThree,             // Gets the first three frames
}
