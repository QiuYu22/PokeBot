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

    [Category(Generate), DisplayName("允许批量命令"), Description("允许用户使用批量命令提交更多自定义内容。")]
    public bool AllowBatchCommands { get; set; } = true;

    [Category(Generate), DisplayName("允许自定义训练家数据"), Description("允许用户在 Showdown 集合中自定义 OT、TID、SID 和 OT 性别。")]
    public bool AllowTrainerDataOverride { get; set; } = true;

    [Category(Generate), DisplayName("禁止非原生宝可梦"), Description("禁止交换需要 HOME 追踪码的宝可梦，即使文件中已包含。")]
    public bool DisallowNonNatives { get; set; } = false;

    [Category(Generate), DisplayName("禁止已有 HOME Tracker"), Description("禁止交换已经拥有 HOME 追踪码的宝可梦。")]
    public bool DisallowTracked { get; set; } = false;

    [Category(Generate), DisplayName("非法集合生成彩蛋"), Description("当提供非法集合时，机器人会生成彩蛋宝可梦。")]
    public bool EnableEasterEggs { get; set; } = false;

    [Category(Generate), DisplayName("启用 HOME 跟踪校验"), Description("对必须在 Switch 游戏间旅行的宝可梦要求具备 HOME 追踪码。")]
    public bool EnableHOMETrackerCheck { get; set; } = false;

    [Category(Generate), DisplayName("50 级视作 100 级"), Description("假定 50 级集合代表 100 级对战集合。")]
    public bool ForceLevel100for50 { get; set; } = true;

    [Category(Generate), DisplayName("强制指定球"), Description("若合法，则强制使用指定的精灵球。")]
    public bool ForceSpecifiedBall { get; set; } = true;

    [Category(Generate), DisplayName("默认语言"), Description("当 PKM 不匹配任何提供的文件时使用的默认语言。")]
    public LanguageID GenerateLanguage { get; set; } = LanguageID.English;

    [Category(Generate), DisplayName("默认 OT 名称"), Description("当 PKM 不匹配提供的训练家数据时使用的默认 OT 名称。")]
    public string GenerateOT
    {
        get => DefaultTrainerName;
        set
        {
            if (!StringsUtil.IsSpammyString(value))
                DefaultTrainerName = value;
        }
    }

    [Category(Generate), DisplayName("训练家数据目录"), Description("用于重新生成 PKM 时读取训练家数据的目录。")]
    public string GeneratePathTrainerInfo { get; set; } = string.Empty;

    [Category(Generate), DisplayName("默认 SID16"), Description("当请求不匹配提供的训练家文件时使用的默认 16 位 SID（5 位数字）。")]
    public ushort GenerateSID16 { get; set; } = 54321;

    [Category(Generate), DisplayName("默认 TID16"), Description("当请求不匹配提供的训练家文件时使用的默认 16 位 TID（5 位数字）。")]
    public ushort GenerateTID16 { get; set; } = 12345;

    // Generate
    [Category(Generate), DisplayName("MGDB 目录"), Description("奇迹卡片 MGDB 的目录路径。")]
    public string MGDBPath { get; set; } = string.Empty;

    [Category(Generate), DisplayName("遭遇优先顺序"), Description("尝试不同遭遇类型的顺序。")]
    public List<EncounterTypeGroup> PrioritizeEncounters { get; set; } =
    [
        EncounterTypeGroup.Slot, EncounterTypeGroup.Egg,
        EncounterTypeGroup.Static, EncounterTypeGroup.Mystery,
        EncounterTypeGroup.Trade,
    ];

    [Category(Generate), DisplayName("优先按照游戏排序"), Description("为 True 时按照 PriorityOrder 搜索遭遇；为 False 时使用最新游戏版本。建议保持 True。")]
    public bool PrioritizeGame { get; set; } = false;

    [Category(Generate), DisplayName("游戏版本优先顺序"), Description("自动合法化 (ALM) 尝试的游戏版本顺序。")]
    public List<GameVersion> PriorityOrder { get; set; } =
        [.. Enum.GetValues<GameVersion>().Where(ver => ver > GameVersion.Any && ver <= (GameVersion)52)];

    // Misc
    [Browsable(false)]
    [Category(Misc), DisplayName("清除 HOME 追踪码"), Description("对克隆或用户请求的 PKM 清零 HOME Tracker，可能导致数据无效，建议保持关闭。")]
    public bool ResetHOMETracker { get; set; } = false;

    [Category(Generate), DisplayName("设置所有合法缎带"), Description("为生成的宝可梦设置全部可能的合法缎带。")]
    public bool SetAllLegalRibbons { get; set; } = false;

    [Browsable(false)]
    [Category(Generate), DisplayName("设置对战版本"), Description("为支持的游戏（仅 SWSH）添加对战版本，以便旧世代宝可梦在线对战。")]
    public bool SetBattleVersion { get; set; } = false;

    [Category(Generate), DisplayName("匹配配色球"), Description("根据颜色为生成的宝可梦设置匹配的精灵球。")]
    public bool SetMatchingBalls { get; set; } = true;

    [Category(Generate), DisplayName("生成超时时间 (秒)"), Description("生成集合时最多等待的秒数，防止复杂集合卡死机器人。")]
    public int Timeout { get; set; } = 15;

    [Category(Misc), DisplayName("使用交易伙伴信息"), Description("将交易伙伴的 OT/SID/TID 应用于有效宝可梦（AutoOT）。")]
    public bool UseTradePartnerInfo { get; set; } = true;

    public override string ToString() => "合法性生成设置";
}
