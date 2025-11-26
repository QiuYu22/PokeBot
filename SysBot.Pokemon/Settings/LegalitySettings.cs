using PKHeX.Core;
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;

namespace SysBot.Pokemon;

public class LegalitySettings
{
    private const string Generate = "生成";

    private const string Misc = "杂项";

    private string DefaultTrainerName = "Ash";

    [Category(Generate), Description("允许用户使用批处理编辑器命令提交进一步的自定义。"), DisplayName("允许批处理命令")]
    public bool AllowBatchCommands { get; set; } = true;

    [Category(Generate), Description("允许用户在 Showdown 格式中提交自定义的 OT、TID、SID 和 OT 性别。"), DisplayName("允许训练家数据覆盖")]
    public bool AllowTrainerDataOverride { get; set; } = true;

    [Category(Generate), Description("禁止交易需要 HOME 追踪器的宝可梦，即使文件已经有一个。"), DisplayName("禁止非本地宝可梦")]
    public bool DisallowNonNatives { get; set; } = false;

    [Category(Generate), Description("禁止交易已经有 HOME 追踪器的宝可梦。"), DisplayName("禁止 HOME 追踪宝可梦")]
    public bool DisallowTracked { get; set; } = false;

    [Category(Generate), Description("如果提供了非法的配置，机器人将创建一个彩蛋宝可梦。"), DisplayName("启用彩蛋")]
    public bool EnableEasterEggs { get; set; } = false;

    [Category(Generate), Description("交易必须在 Switch 游戏之间传输过的宝可梦时需要 HOME 追踪器。"), DisplayName("启用 HOME 追踪检查")]
    public bool EnableHOMETrackerCheck { get; set; } = false;

    [Category(Generate), Description("假设 50 级的配置是 100 级的对战配置。"), DisplayName("50 级强制为 100 级")]
    public bool ForceLevel100for50 { get; set; } = true;

    [Category(Generate), Description("如果合法，强制使用指定的精灵球。"), DisplayName("强制指定精灵球")]
    public bool ForceSpecifiedBall { get; set; } = true;

    [Category(Generate), Description("与任何提供的 PKM 文件都不匹配时使用的默认语言。"), DisplayName("生成语言")]
    public LanguageID GenerateLanguage { get; set; } = LanguageID.English;

    [Category(Generate), Description("与任何提供的 PKM 文件都不匹配时使用的默认原训练家名称。"), DisplayName("生成 OT")]
    public string GenerateOT
    {
        get => DefaultTrainerName;
        set
        {
            if (!StringsUtil.IsSpammyString(value))
                DefaultTrainerName = value;
        }
    }

    [Category(Generate), Description("包含用于重新生成 PKM 文件的训练家数据的 PKM 文件夹。"), DisplayName("训练家数据路径")]
    public string GeneratePathTrainerInfo { get; set; } = string.Empty;

    [Category(Generate), Description("与任何提供的训练家数据文件都不匹配的请求使用的默认 16 位里ID (SID)。应为 5 位数字。"), DisplayName("生成 SID16")]
    public ushort GenerateSID16 { get; set; } = 54321;

    [Category(Generate), Description("与任何提供的训练家数据文件都不匹配的请求使用的默认 16 位表ID (TID)。应为 5 位数字。"), DisplayName("生成 TID16")]
    public ushort GenerateTID16 { get; set; } = 12345;

    // 生成
    [Category(Generate), Description("神秘礼物的 MGDB 目录路径。"), DisplayName("MGDB 路径")]
    public string MGDBPath { get; set; } = string.Empty;

    [Category(Generate), Description("尝试宝可梦遭遇类型的顺序。"), DisplayName("优先遭遇类型")]
    public List<EncounterTypeGroup> PrioritizeEncounters { get; set; } =
    [
        EncounterTypeGroup.Slot, EncounterTypeGroup.Egg,
        EncounterTypeGroup.Static, EncounterTypeGroup.Mystery,
        EncounterTypeGroup.Trade,
    ];

    [Category(Generate), Description("如果 PrioritizeGame 设置为 \"True\"，则使用 PriorityOrder 开始查找遭遇。如果为 \"False\"，则使用最新游戏作为版本。建议保持为 \"True\"。"), DisplayName("优先游戏")]
    public bool PrioritizeGame { get; set; } = false;

    [Category(Generate), Description("ALM 尝试合法化的游戏版本顺序。"), DisplayName("游戏版本优先顺序")]
    public List<GameVersion> PriorityOrder { get; set; } =
        [.. Enum.GetValues<GameVersion>().Where(ver => ver > GameVersion.Any && ver <= (GameVersion)52)];

    // 杂项
    [Browsable(false)]
    [Category(Misc), Description("将克隆和用户请求的 PKM 文件的 HOME 追踪器清零。建议保持禁用以避免创建无效的 HOME 数据。"), DisplayName("重置 HOME 追踪器")]
    public bool ResetHOMETracker { get; set; } = false;

    [Category(Generate), Description("为任何生成的宝可梦设置所有可能的合法奖章。"), DisplayName("设置所有合法奖章")]
    public bool SetAllLegalRibbons { get; set; } = false;

    [Browsable(false)]
    [Category(Generate), Description("为支持它的游戏（仅限 SWSH）添加对战版本，以便在在线对战中使用过去世代的宝可梦。"), DisplayName("设置对战版本")]
    public bool SetBattleVersion { get; set; } = false;

    [Category(Generate), Description("为任何生成的宝可梦设置匹配的精灵球（基于颜色）。"), DisplayName("设置匹配精灵球")]
    public bool SetMatchingBalls { get; set; } = true;

    [Category(Generate), Description("生成配置时在取消前花费的最长时间（秒）。这可以防止困难的配置冻结机器人。"), DisplayName("超时时间")]
    public int Timeout { get; set; } = 15;

    [Category(Misc), Description("使用训练家的 OT/SID/TID 应用有效的宝可梦 (AutoOT)"), DisplayName("使用交易伙伴信息")]
    public bool UseTradePartnerInfo { get; set; } = true;

    public override string ToString() => "合法性生成设置";
}
