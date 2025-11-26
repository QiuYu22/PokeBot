using PKHeX.Core;
using SysBot.Base;
using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class DistributionSettings : ISynchronizationSetting, ICustomTypeDescriptor
{
    private const string Distribute = "分发";

    private const string Synchronize = "同步";

    [Browsable(false)]
    public ProgramMode CurrentMode { get; set; } = ProgramMode.None;

    [Category(Distribute), Description("启用后，空闲的连接交易机器人将从分发文件夹中随机分发 PKM 文件。"), DisplayName("空闲时分发")]
    public bool DistributeWhileIdle { get; set; } = true;

    [Category(Distribute), Description("设置为 true 时，随机 Ledy 昵称交换交易将退出而不是从池中交易随机实体。"), DisplayName("Ledy 无匹配时退出")]
    public bool LedyQuitIfNoMatch { get; set; }

    [Category(Distribute), Description("设置为 None 以外的值时，随机交易除了昵称匹配外还需要匹配此物种。"), DisplayName("Ledy 物种")]
    public Species LedySpecies { get; set; } = Species.None;

    [Category(Distribute), Description("分发交易连接密码使用最小和最大范围而不是固定交易密码。"), DisplayName("随机密码")]
    public bool RandomCode { get; set; }

    [Category(Distribute), Description("对于 LGPE，用于分发交易的第一个图片密码。"), DisplayName("LGPE 图片密码 1")]
    public Pictocodes LGPECode1 { get; set; } = Pictocodes.Pikachu;

    [Category(Distribute), Description("对于 LGPE，用于分发交易的第二个图片密码。"), DisplayName("LGPE 图片密码 2")]
    public Pictocodes LGPECode2 { get; set; } = Pictocodes.Pikachu;

    [Category(Distribute), Description("对于 LGPE，用于分发交易的第三个图片密码。"), DisplayName("LGPE 图片密码 3")]
    public Pictocodes LGPECode3 { get; set; } = Pictocodes.Pikachu;

    [Category(Distribute), Description("对于 BDSP，分发机器人将进入特定房间并停留在那里直到机器人停止。"), DisplayName("BDSP 保持在联盟房间")]
    public bool RemainInUnionRoomBDSP { get; set; } = true;

    // 分发
    [Category(Distribute), Description("启用后，分发文件夹将随机产出而不是按相同顺序。"), DisplayName("随机顺序")]
    public bool Shuffled { get; set; }

    [Category(Synchronize), Description("连接交易：使用多个分发机器人时 - 所有机器人将同时确认其交易密码。设置为本地时，当所有机器人都在屏障处时将继续。设置为远程时，需要其他东西发出信号让机器人继续。"), DisplayName("同步机器人")]
    public BotSyncOption SynchronizeBots { get; set; } = BotSyncOption.LocalSync;

    // 同步
    [Category(Synchronize), Description("连接交易：使用多个分发机器人时 - 一旦所有机器人准备好确认交易密码，Hub 将等待 X 毫秒后释放所有机器人。"), DisplayName("同步屏障延迟")]
    public int SynchronizeDelayBarrier { get; set; }

    [Category(Synchronize), Description("连接交易：使用多个分发机器人时 - 机器人等待同步的时间（秒），超时后将继续执行。"), DisplayName("同步超时")]
    public double SynchronizeTimeout { get; set; } = 90;

    [Category(Distribute), Description("分发交易连接密码。"), DisplayName("交易密码")]
    public int TradeCode { get; set; } = 7196;

    public override string ToString() => "分发交易设置";

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
