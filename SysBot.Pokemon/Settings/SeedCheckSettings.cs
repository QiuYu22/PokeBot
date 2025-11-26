using System.ComponentModel;

namespace SysBot.Pokemon;

public class SeedCheckSettings
{
    private const string FeatureToggle = "功能开关";

    [Category(FeatureToggle), Description("允许只返回最近的闪光帧、第一个星闪和方闪帧，或前三个闪光帧。"), DisplayName("结果显示模式")]
    public SeedCheckResults ResultDisplayMode { get; set; }

    [Category(FeatureToggle), Description("启用后，种子检查将返回所有可能的种子结果，而不是第一个有效匹配。"), DisplayName("显示所有 Z3 结果")]
    public bool ShowAllZ3Results { get; set; }

    public override string ToString() => "种子检查设置";
}

public enum SeedCheckResults
{
    ClosestOnly,            // Only gets the first shiny

    FirstStarAndSquare,     // Gets the first star shiny and first square shiny

    FirstThree,             // Gets the first three frames
}
