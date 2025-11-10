using PKHeX.Core;
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;

namespace SysBot.Pokemon;

public class LegalitySettings
{
    private const string Generate = "生成设置";

    private const string Misc = "其他选项";

    private string DefaultTrainerName = "Ash";

    [DisplayName("允许批量命令")]
    [Category(Generate), Description("允许用户通过批量编辑命令提交更多自定义内容。")]
    public bool AllowBatchCommands { get; set; } = true;

    [DisplayName("允许覆盖训练家数据")]
    [Category(Generate), Description("允许用户在 Showdown 配置中自定义 OT、TID、SID 以及训练家性别。")]
    public bool AllowTrainerDataOverride { get; set; } = true;

    [Category(Generate), Description("阻止需要 HOME 追踪编号的宝可梦进行交易，即使文件已包含该追踪。"), DisplayName("禁止非原生宝可梦")]
    public bool DisallowNonNatives { get; set; } = false;

    [Category(Generate), Description("阻止已带有 HOME 追踪编号的宝可梦进行交易。"), DisplayName("禁止已追踪宝可梦")]
    public bool DisallowTracked { get; set; } = false;

    [DisplayName("启用彩蛋宝可梦")]
    [Category(Generate), Description("当收到非法配置时由机器人生成彩蛋宝可梦。")]
    public bool EnableEasterEggs { get; set; } = false;

    [DisplayName("启用 HOME 追踪检测")]
    [Category(Generate), Description("在交易需穿梭于 Switch 游戏之间的宝可梦时强制要求 HOME 追踪编号。")]
    public bool EnableHOMETrackerCheck { get; set; } = false;

    [DisplayName("等级 50 视为 100")]
    [Category(Generate), Description("将等级 50 的配置视为等级 100 的对战配置。")]
    public bool ForceLevel100for50 { get; set; } = true;

    [DisplayName("强制使用指定精灵球")]
    [Category(Generate), Description("若合法，则强制使用指定的精灵球。")]
    public bool ForceSpecifiedBall { get; set; } = true;

    [DisplayName("默认训练家语言")]
    [Category(Generate), Description("当未找到匹配的训练家数据时使用的默认语言。")]
    public LanguageID GenerateLanguage { get; set; } = LanguageID.English;

    [DisplayName("默认训练家名称")]
    [Category(Generate), Description("当未找到匹配的训练家数据时使用的默认训练家名称。")]
    public string GenerateOT
    {
        get => DefaultTrainerName;
        set
        {
            if (!StringsUtil.IsSpammyString(value))
                DefaultTrainerName = value;
        }
    }

    [DisplayName("训练家数据目录")]
    [Category(Generate), Description("包含训练家数据的 PKM 文件目录，用于再生成时引用。")]
    public string GeneratePathTrainerInfo { get; set; } = string.Empty;

    [DisplayName("默认 SID16")]
    [Category(Generate), Description("当未匹配训练家数据时使用的默认 16 位 SID（5 位数）。")]
    public ushort GenerateSID16 { get; set; } = 54321;

    [DisplayName("默认 TID16")]
    [Category(Generate), Description("当未匹配训练家数据时使用的默认 16 位 TID（5 位数）。")]
    public ushort GenerateTID16 { get; set; } = 12345;

    // Generate
    [DisplayName("Wonder Card 目录")]
    [Category(Generate), Description("Wonder Card（MGDB）目录路径。")]
    public string MGDBPath { get; set; } = string.Empty;

    [DisplayName("遭遇方式优先级")]
    [Category(Generate), Description("尝试各种遭遇方式的优先顺序。")]
    public List<EncounterTypeGroup> PrioritizeEncounters { get; set; } =
    [
        EncounterTypeGroup.Slot, EncounterTypeGroup.Egg,
        EncounterTypeGroup.Static, EncounterTypeGroup.Mystery,
        EncounterTypeGroup.Trade,
    ];

    [DisplayName("按游戏优先")]
    [Category(Generate), Description("当 PrioritizeGame 为 True 时，按 PriorityOrder 的顺序寻找遭遇；为 False 时将使用最新游戏版本。建议保持为 True。")]
    public bool PrioritizeGame { get; set; } = false;

    [DisplayName("游戏优先顺序")]
    [Category(Generate), Description("ALM 尝试合法化时的游戏版本优先顺序。")]
    public List<GameVersion> PriorityOrder { get; set; } =
        [.. Enum.GetValues<GameVersion>().Where(ver => ver > GameVersion.Any && ver <= (GameVersion)52)];

    // Misc
    [Browsable(false)]
    [Category(Misc), Description("对克隆或用户请求的宝可梦清零 HOME 追踪编号。为避免无效的 HOME 数据，建议保持禁用。")]
    public bool ResetHOMETracker { get; set; } = false;

    [DisplayName("授予所有合法缎带")]
    [Category(Generate), Description("为生成的宝可梦设置所有合法的缎带。")]
    public bool SetAllLegalRibbons { get; set; } = false;

    [Browsable(false)]
    [Category(Generate), Description("为支持的游戏添加对战版本（仅 SWSH），以便旧世代宝可梦参与线上对战。")]
    public bool SetBattleVersion { get; set; } = false;

    [DisplayName("匹配宝可梦精灵球")]
    [Category(Generate), Description("根据配色为生成的宝可梦选择匹配的精灵球。")]
    public bool SetMatchingBalls { get; set; } = true;

    [DisplayName("生成超时时间（秒）")]
    [Category(Generate), Description("生成单个配置前允许的最大耗时（秒），避免复杂配置卡住机器人。")]
    public int Timeout { get; set; } = 15;

    [DisplayName("自ID")]
    [Category(Misc), Description("将有效宝可梦的 OT/SID/TID 自动匹配为交易方信息（AutoOT）。")]
    public bool UseTradePartnerInfo { get; set; } = true;

    public override string ToString() => "合法性生成设置";
}
