using System.ComponentModel;

namespace SysBot.Pokemon;

public class FossilSettings
{
    private const string Counts = "数量";

    private const string Fossil = "化石";

    /// <summary>
    /// Toggle for injecting fossil pieces.
    /// </summary>
    [Category(Fossil), DisplayName("化石补充开关"), Description("是否在耗尽时自动注入化石碎片。")]
    public bool InjectWhenEmpty { get; set; }

    [Category(Fossil), DisplayName("目标化石物种"), Description("需要复苏的化石宝可梦物种。")]
    public FossilSpecies Species { get; set; } = FossilSpecies.Dracozolt;

    public override string ToString() => "化石机器人设置";
}
