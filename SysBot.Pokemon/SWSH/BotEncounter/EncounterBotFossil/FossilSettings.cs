using System.ComponentModel;

namespace SysBot.Pokemon;

public class FossilSettings
{
    private const string Counts = nameof(Counts);

    private const string Fossil = nameof(Fossil);

    /// <summary>
    /// Toggle for injecting fossil pieces.
    /// </summary>
    [Category(Fossil), Description("是否在耗尽时自动注入化石碎片。")]
    public bool InjectWhenEmpty { get; set; }

    [Category(Fossil), Description("要猎捕的化石宝可梦种类。")]
    public FossilSpecies Species { get; set; } = FossilSpecies.Dracozolt;

    public override string ToString() => "化石机器人设置";
}
