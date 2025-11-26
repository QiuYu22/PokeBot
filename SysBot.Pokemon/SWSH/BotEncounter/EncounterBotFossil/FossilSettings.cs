using System.ComponentModel;

namespace SysBot.Pokemon;

public class FossilSettings
{
    private const string Counts = "统计";

    private const string Fossil = "化石";

    /// <summary>
    /// Toggle for injecting fossil pieces.
    /// </summary>
    [Category(Fossil), Description("注入化石碎片的开关。"), DisplayName("空时注入")]
    public bool InjectWhenEmpty { get; set; }

    [Category(Fossil), Description("要寻找的化石宝可梦物种。"), DisplayName("物种")]
    public FossilSpecies Species { get; set; } = FossilSpecies.Dracozolt;

    public override string ToString() => "化石机器人设置";
}
