using PKHeX.Core;
using SysBot.Base;
using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class DistributionSettings : ISynchronizationSetting, ICustomTypeDescriptor
{
    private const string Distribute = "派发";

    private const string Synchronize = "同步";

    [Browsable(false)]
    public ProgramMode CurrentMode { get; set; } = ProgramMode.None;

    [Category(Distribute), DisplayName("闲置时派发文件"), Description("启用后，闲置的交易机器人会随机派发派发文件夹中的 PKM。")]
    public bool DistributeWhileIdle { get; set; } = true;

    [Category(Distribute), DisplayName("Ledy 无匹配时退出"), Description("启用后，随机 Ledy 昵称交换若找不到匹配会直接退出，而不是发送数据库中随机对象。")]
    public bool LedyQuitIfNoMatch { get; set; }

    [Category(Distribute), DisplayName("Ledy 指定物种"), Description("随机交易除昵称匹配外，还要求此物种（若不为 None）。")]
    public Species LedySpecies { get; set; } = Species.None;

    [Category(Distribute), DisplayName("使用随机连线代码"), Description("派发交易的连线代码使用最小/最大范围，而非固定代码。")]
    public bool RandomCode { get; set; }

    [Category(Distribute), DisplayName("LGPE 图像代码 1"), Description("LGPE 派发交易使用的第一个 Picto 代码。")]
    public Pictocodes LGPECode1 { get; set; } = Pictocodes.Pikachu;

    [Category(Distribute), DisplayName("LGPE 图像代码 2"), Description("LGPE 派发交易使用的第二个 Picto 代码。")]
    public Pictocodes LGPECode2 { get; set; } = Pictocodes.Pikachu;

    [Category(Distribute), DisplayName("LGPE 图像代码 3"), Description("LGPE 派发交易使用的第三个 Picto 代码。")]
    public Pictocodes LGPECode3 { get; set; } = Pictocodes.Pikachu;

    [Category(Distribute), DisplayName("BDSP 停留在房间"), Description("BDSP 派发机器人前往指定房间并停留至停止。")]
    public bool RemainInUnionRoomBDSP { get; set; } = true;

    // Distribute
    [Category(Distribute), DisplayName("随机派发顺序"), Description("启用后派发文件夹会随机选择，而非固定顺序。")]
    public bool Shuffled { get; set; }

    [Category(Synchronize), DisplayName("同步模式"), Description("多台派发机器人进行 Link Trade 时的同步方式。Local 表示所有机器人到达屏障后继续；Remote 需要外部信号。")]
    public BotSyncOption SynchronizeBots { get; set; } = BotSyncOption.LocalSync;

    // Synchronize
    [Category(Synchronize), DisplayName("屏障释放延迟 (ms)"), Description("多台派发机器人就绪后，在释放前等待的毫秒数。")]
    public int SynchronizeDelayBarrier { get; set; }

    [Category(Synchronize), DisplayName("同步等待超时 (秒)"), Description("机器人等待同步的最长秒数，超过后将自行继续。")]
    public double SynchronizeTimeout { get; set; } = 90;

    [Category(Distribute), DisplayName("派发连线代码"), Description("派发交易使用的连线代码。")]
    public int TradeCode { get; set; } = 7196;

    public override string ToString() => "派发交易设置";

    // Visibility control methods for JSON serialization
    public bool ShouldSerializeTradeCode() => CurrentMode != ProgramMode.LGPE;
    public bool ShouldSerializeLGPECode1() => CurrentMode == ProgramMode.LGPE;
    public bool ShouldSerializeLGPECode2() => CurrentMode == ProgramMode.LGPE;
    public bool ShouldSerializeLGPECode3() => CurrentMode == ProgramMode.LGPE;

    // ICustomTypeDescriptor implementation for PropertyGrid visibility
    public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(this, true);
    public string? GetClassName() => TypeDescriptor.GetClassName(this, true);
    public string? GetComponentName() => TypeDescriptor.GetComponentName(this, true);
    public TypeConverter? GetConverter() => TypeDescriptor.GetConverter(this, true);
    public EventDescriptor? GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);
    public PropertyDescriptor? GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this, true);
    public object? GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType, true);
    public EventDescriptorCollection GetEvents() => TypeDescriptor.GetEvents(this, true);
    public EventDescriptorCollection GetEvents(Attribute[]? attributes) => TypeDescriptor.GetEvents(this, attributes, true);

    public PropertyDescriptorCollection GetProperties() => GetProperties(null);

    public PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        var properties = TypeDescriptor.GetProperties(this, attributes, true);
        var filtered = properties.Cast<PropertyDescriptor>().Where(prop =>
        {
            // Hide TradeCode when in LGPE mode
            if (prop.Name == nameof(TradeCode) && CurrentMode == ProgramMode.LGPE)
                return false;

            // Show LGPE codes only when in LGPE mode
            if (prop.Name == nameof(LGPECode1) && CurrentMode != ProgramMode.LGPE)
                return false;
            if (prop.Name == nameof(LGPECode2) && CurrentMode != ProgramMode.LGPE)
                return false;
            if (prop.Name == nameof(LGPECode3) && CurrentMode != ProgramMode.LGPE)
                return false;

            return true;
        }).ToArray();

        return new PropertyDescriptorCollection(filtered);
    }

    public object? GetPropertyOwner(PropertyDescriptor? pd) => this;
}
