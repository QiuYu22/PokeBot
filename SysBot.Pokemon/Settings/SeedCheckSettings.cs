using System.ComponentModel;

namespace SysBot.Pokemon;

public class SeedCheckSettings
{
    private const string FeatureToggle = "功能设置";

    [DisplayName("结果显示模式")]
    [Category(FeatureToggle), Description("选择返回最近的闪光帧、首个星/方闪光帧，或前三个闪光帧。")]
    public SeedCheckResults ResultDisplayMode { get; set; }

    [DisplayName("显示全部结果")]
    [Category(FeatureToggle), Description("启用后，种子检测将返回所有可能的结果，而非仅第一个匹配项。")]
    public bool ShowAllZ3Results { get; set; }

    public override string ToString() => "种子检测设置";
}

public enum SeedCheckResults
{
    ClosestOnly,            // Only gets the first shiny

    FirstStarAndSquare,     // Gets the first star shiny and first square shiny

    FirstThree,             // Gets the first three frames
}
