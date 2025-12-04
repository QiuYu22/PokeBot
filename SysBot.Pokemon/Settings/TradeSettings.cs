using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class TradeSettings : IBotStateSettings, ICountSettings
{
    private const string CountStats = "统计";

    private const string HOMELegality = "HOME 合法性";

    private const string TradeConfig = "交易配置";

    private const string VGCPastesConfig = "VGC 贴文配置";

    private const string Miscellaneous = "杂项";

    private const string RequestFolders = "请求文件夹";

    private const string EmbedSettings = "嵌入设置";

    public override string ToString() => "交易配置设置";

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class EmojiInfo
    {
        [Description("Emoji 的完整字符串。"), DisplayName("Emoji 字符串")]
        public string EmojiString { get; set; } = string.Empty;

        public override string ToString()
        {
            return string.IsNullOrEmpty(EmojiString) ? "未设置" : EmojiString;
        }
    }

    [Category(TradeConfig), Description("交易配置相关设置。"), DisplayName("交易配置"), Browsable(true)]
    public TradeSettingsCategory TradeConfiguration { get; set; } = new();

    [Category(EmbedSettings), Description("Discord 交易嵌入相关设置。"), DisplayName("交易嵌入设置"), Browsable(true)]
    public TradeEmbedSettingsCategory TradeEmbedSettings { get; set; } = new();

    [Category(RequestFolders), Description("请求文件夹相关设置。"), DisplayName("请求文件夹设置"), Browsable(true)]
    public RequestFolderSettingsCategory RequestFolderSettings { get; set; } = new();

    [Category(CountStats), Description("交易计数统计相关设置。"), DisplayName("交易统计设置"), Browsable(true)]
    public CountStatsSettingsCategory CountStatsSettings { get; set; } = new();

    [Category(TradeConfig), TypeConverter(typeof(CategoryConverter<TradeSettingsCategory>))]
    public class TradeSettingsCategory
    {
        public override string ToString() => "交易配置";

        [Category(TradeConfig), Description("允许的最小连接代码。"), DisplayName("最小连接代码")]
        public int MinTradeCode { get; set; } = 0;

        [Category(TradeConfig), Description("允许的最大连接代码。"), DisplayName("最大连接代码")]
        public int MaxTradeCode { get; set; } = 9999_9999;

        [Category(TradeConfig), Description("启用后，Discord 用户的交易代码会被保存并重复使用。"), DisplayName("保存并复用交易代码")]
        public bool StoreTradeCodes { get; set; } = true;

        [Category(TradeConfig), Description("等待交易伙伴的时间（秒）。"), DisplayName("交易伙伴等待时间(秒)")]
        public int TradeWaitTime { get; set; } = 30;

        [Category(TradeConfig), Description("按下 A 后等待交易处理的最长时间（秒）。"), DisplayName("最大交易确认时间(秒)")]
        public int MaxTradeConfirmTime { get; set; } = 25;

        [Category(TradeConfig), Description("道具交易模式下使用的默认宝可梦。"), DisplayName("物品交易默认宝可梦")]
        public Species ItemTradeSpecies { get; set; } = Species.None;

        [Category(TradeConfig), Description("未指定时发送的默认携带物。"), DisplayName("默认携带物")]
        public HeldItem DefaultHeldItem { get; set; } = HeldItem.None;

        [Category(TradeConfig), Description("启用后，合法宝可梦默认附带建议的回忆技能，无需批量命令。"), DisplayName("默认建议回忆技能")]
        public bool SuggestRelearnMoves { get; set; } = true;

        [Category(TradeConfig), Description("允许或禁止批量交易。"), DisplayName("允许批量交易")]
        public bool AllowBatchTrades { get; set; } = true;

        [Category(TradeConfig), Description("检查昵称与 OT 是否包含垃圾信息。"), DisplayName("启用垃圾检查")]
        public bool EnableSpamCheck { get; set; } = true;

        [Category(TradeConfig), Description("单次交易可发送的最大宝可梦数量，若小于 1 则关闭批量模式。"), DisplayName("单次交易最大数量")]
        public int MaxPkmsPerTrade { get; set; } = 1;

        [Category(TradeConfig), Description("转储交易：单个用户达到最大转储次数后终止。"), DisplayName("单次交易最大转储数")]
        public int MaxDumpsPerTrade { get; set; } = 20;

        [Category(TradeConfig), Description("转储交易：在交易中等待指定秒数后终止。"), DisplayName("最大转储交易时间(秒)")]
        public int MaxDumpTradeTime { get; set; } = 45;

        [Category(TradeConfig), Description("转储交易：启用后向用户输出合法性检查信息。"), DisplayName("转储交易合法性检查")]
        public bool DumpTradeLegalityCheck { get; set; } = true;

        [Category(TradeConfig), Description("若对方提供会在交易中进化的宝可梦时自动取消交易。"), DisplayName("禁止交易进化")]
        public bool DisallowTradeEvolve { get; set; } = true;

        [Category(TradeConfig), DisplayName("LGPE 动画最大延迟(秒)"), Description("LGPE 特有设置，控制交易动画的最长等待时间。")]
        public int TradeAnimationMaxDelaySeconds { get; set; } = 25;

        public enum HeldItem
        {
            None = 0,

            MasterBall = 1,

            RareCandy = 50,

            ppUp = 51,

            ppMax = 53,

            BigPearl = 89,

            Nugget = 92,

            AbilityCapsule = 645,

            BottleCap = 795,

            GoldBottleCap = 796,

            expCandyL = 1127,

            expCandyXL = 1128,

            AbilityPatch = 1606,

            FreshStartMochi = 2479,
        }
    }

    [Category(EmbedSettings), TypeConverter(typeof(CategoryConverter<TradeEmbedSettingsCategory>))]
    public class TradeEmbedSettingsCategory
    {
        public override string ToString() => "交易嵌入配置";

        private bool _useEmbeds = true;

        [Category(EmbedSettings), Description("启用后在 Discord 交易频道展示美观的嵌入信息，否则使用默认文本。"), DisplayName("启用嵌入展示")]
        public bool UseEmbeds
        {
            get => _useEmbeds;
            set
            {
                _useEmbeds = value;
                OnUseEmbedsChanged();
            }
        }

        private void OnUseEmbedsChanged()
        {
            if (!_useEmbeds)
            {
                PreferredImageSize = ImageSize.Size256x256;
                MoveTypeEmojis = false;
                ShowScale = false;
                ShowTeraType = false;
                ShowLevel = false;
                ShowMetDate = false;
                ShowAbility = false;
                ShowNature = false;
                ShowIVs = false;
            }
        }

        [Category(EmbedSettings), Description("嵌入中物种图片的首选尺寸。"), DisplayName("物种图片尺寸")]
        public ImageSize PreferredImageSize { get; set; } = ImageSize.Size256x256;

        [Category(EmbedSettings), Description("在交易嵌入中显示招式属性图标（仅 Discord，需要自备表情）。"), DisplayName("显示招式属性表情")]
        public bool MoveTypeEmojis { get; set; } = true;

        [Category(EmbedSettings), Description("自定义各属性使用的表情。"), DisplayName("自定义属性表情")]
        public List<MoveTypeEmojiInfo> CustomTypeEmojis { get; set; } =
        [
            new(MoveType.Bug),
            new(MoveType.Fire),
            new(MoveType.Flying),
            new(MoveType.Ground),
            new(MoveType.Water),
            new(MoveType.Grass),
            new(MoveType.Ice),
            new(MoveType.Rock),
            new(MoveType.Ghost),
            new(MoveType.Steel),
            new(MoveType.Fighting),
            new(MoveType.Electric),
            new(MoveType.Dragon),
            new(MoveType.Psychic),
            new(MoveType.Dark),
            new(MoveType.Normal),
            new(MoveType.Poison),
            new(MoveType.Fairy),
            new(MoveType.Stellar)
        ];

        [Category(EmbedSettings), Description("男性性别表情的完整字符串。"), DisplayName("男性表情")]
        public EmojiInfo MaleEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("女性性别表情的完整字符串。"), DisplayName("女性表情")]
        public EmojiInfo FemaleEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("显示神秘礼物状态的表情配置。"), DisplayName("神秘礼物表情")]
        public EmojiInfo MysteryGiftEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("显示 Alpha 印记的表情配置。"), DisplayName("头目印记表情")]
        public EmojiInfo AlphaMarkEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("显示最强印记的表情配置。"), DisplayName("最强印记表情")]
        public EmojiInfo MightiestMarkEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("阿尔宙斯传说中的 Alpha（头目） 表情的配置。"), DisplayName("PLA 头目表情")]
        public EmojiInfo AlphaPLAEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("在交易嵌入中显示太晶属性图标（仅 Discord，需要自备表情）。"), DisplayName("显示太晶属性表情")]
        public bool UseTeraEmojis { get; set; } = true;

        [Category(EmbedSettings), Description("自定义各太晶属性的表情。"), DisplayName("自定义太晶表情")]
        public List<TeraTypeEmojiInfo> TeraTypeEmojis { get; set; } =
        [
            new(MoveType.Bug),
            new(MoveType.Fire),
            new(MoveType.Flying),
            new(MoveType.Ground),
            new(MoveType.Water),
            new(MoveType.Grass),
            new(MoveType.Ice),
            new(MoveType.Rock),
            new(MoveType.Ghost),
            new(MoveType.Steel),
            new(MoveType.Fighting),
            new(MoveType.Electric),
            new(MoveType.Dragon),
            new(MoveType.Psychic),
            new(MoveType.Dark),
            new(MoveType.Normal),
            new(MoveType.Poison),
            new(MoveType.Fairy),
            new(MoveType.Stellar)
        ];

        [Category(EmbedSettings), Description("在嵌入信息中显示体型（仅 SV & Discord，需自备表情）。"), DisplayName("显示体型")]
        public bool ShowScale { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入信息中显示太晶属性（SV & Discord）。"), DisplayName("显示太晶属性")]
        public bool ShowTeraType { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入信息中显示等级（仅 Discord）。"), DisplayName("显示等级")]
        public bool ShowLevel { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入信息中显示相遇日期（仅 Discord）。"), DisplayName("显示相遇日期")]
        public bool ShowMetDate { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入信息中显示特性（仅 Discord）。"), DisplayName("显示特性")]
        public bool ShowAbility { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入信息中显示性格（仅 Discord）。"), DisplayName("显示性格")]
        public bool ShowNature { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入信息中显示语言（仅 Discord）。"), DisplayName("显示语言")]
        public bool ShowLanguage { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入信息中显示个体值（仅 Discord）。"), DisplayName("显示 IV")]
        public bool ShowIVs { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入信息中显示努力值（仅 Discord）。"), DisplayName("显示 EV")]
        public bool ShowEVs { get; set; } = true;
    }

    [Category(RequestFolders), TypeConverter(typeof(CategoryConverter<RequestFolderSettingsCategory>))]
    public class RequestFolderSettingsCategory
    {
        public override string ToString() => "请求文件夹设置";

        [Category(RequestFolders), Description("事件文件夹路径（建立 events 文件夹后填入路径）。"), DisplayName("事件文件夹路径")]
        public string EventsFolder { get; set; } = string.Empty;

        [Category(RequestFolders), Description("对战就绪文件夹路径（建立 battleready 文件夹后填入路径）。"), DisplayName("对战就绪文件夹路径")]
        public string BattleReadyPKMFolder { get; set; } = string.Empty;
    }

    [Category(Miscellaneous)]
    [Description("在交易过程中关闭 Switch 屏幕。")]
    [DisplayName("交易时关闭屏幕")]
    public bool ScreenOff { get; set; } = false;

    /// <summary>
    /// Gets a random trade code based on the range settings.
    /// </summary>
    public int GetRandomTradeCode() => Util.Rand.Next(TradeConfiguration.MinTradeCode, TradeConfiguration.MaxTradeCode + 1);

    public static List<Pictocodes> GetRandomLGTradeCode(bool randomtrade = false)
    {
        var lgcode = new List<Pictocodes>();
        if (randomtrade)
        {
            for (int i = 0; i <= 2; i++)
            {
                // code.Add((pictocodes)Util.Rand.Next(10));
                lgcode.Add(Pictocodes.Pikachu);
            }
        }
        else
        {
            for (int i = 0; i <= 2; i++)
            {
                lgcode.Add((Pictocodes)Util.Rand.Next(10));

                // code.Add(pictocodes.Pikachu);
            }
        }
        return lgcode;
    }

    [Category(CountStats), TypeConverter(typeof(CategoryConverter<CountStatsSettingsCategory>))]
    public class CountStatsSettingsCategory
    {
        public override string ToString() => "交易统计";

        private int _completedSurprise;

        private int _completedDistribution;

        private int _completedTrades;

        private int _completedSeedChecks;

        private int _completedClones;

        private int _completedDumps;

        private int _completedFixOTs;

        [Category(CountStats), Description("已完成的惊喜交换次数。"), DisplayName("惊喜交换完成量")]
        public int CompletedSurprise
        {
            get => _completedSurprise;
            set => _completedSurprise = value;
        }

        [Category(CountStats), Description("已完成的派发链接交易次数。"), DisplayName("派发交易完成量")]
        public int CompletedDistribution
        {
            get => _completedDistribution;
            set => _completedDistribution = value;
        }

        [Category(CountStats), Description("已完成的指定用户链接交易次数。"), DisplayName("普通交易完成量")]
        public int CompletedTrades
        {
            get => _completedTrades;
            set => _completedTrades = value;
        }

        [Category(CountStats), Description("已完成的 FixOT 交易次数。"), DisplayName("修复OT 完成量")]
        public int CompletedFixOTs
        {
            get => _completedFixOTs;
            set => _completedFixOTs = value;
        }

        [Browsable(false)]
        [Category(CountStats), Description("已完成的种子检查交易次数。"), DisplayName("种子检查完成量")]
        public int CompletedSeedChecks
        {
            get => _completedSeedChecks;
            set => _completedSeedChecks = value;
        }

        [Category(CountStats), Description("已完成的克隆交易次数。"), DisplayName("克隆交易完成量")]
        public int CompletedClones
        {
            get => _completedClones;
            set => _completedClones = value;
        }

        [Category(CountStats), Description("已完成的转储交易次数。"), DisplayName("转储交易完成量")]
        public int CompletedDumps
        {
            get => _completedDumps;
            set => _completedDumps = value;
        }

        [Category(CountStats), Description("启用后，在请求状态检查时输出这些计数。"), DisplayName("状态检查时输出计数")]
        public bool EmitCountsOnStatusCheck { get; set; }

        public void AddCompletedTrade() => Interlocked.Increment(ref _completedTrades);

        public void AddCompletedSeedCheck() => Interlocked.Increment(ref _completedSeedChecks);

        public void AddCompletedSurprise() => Interlocked.Increment(ref _completedSurprise);

        public void AddCompletedDistribution() => Interlocked.Increment(ref _completedDistribution);

        public void AddCompletedDumps() => Interlocked.Increment(ref _completedDumps);

        public void AddCompletedClones() => Interlocked.Increment(ref _completedClones);

        public void AddCompletedFixOTs() => Interlocked.Increment(ref _completedFixOTs);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (!EmitCountsOnStatusCheck)
                yield break;
            if (CompletedSeedChecks != 0)
                yield return $"种子检查：{CompletedSeedChecks}";
            if (CompletedClones != 0)
                yield return $"克隆交易：{CompletedClones}";
            if (CompletedDumps != 0)
                yield return $"转储交易：{CompletedDumps}";
            if (CompletedTrades != 0)
                yield return $"链接交易：{CompletedTrades}";
            if (CompletedDistribution != 0)
                yield return $"派发交易：{CompletedDistribution}";
            if (CompletedFixOTs != 0)
                yield return $"修复OT：{CompletedFixOTs}";
            if (CompletedSurprise != 0)
                yield return $"惊喜交换：{CompletedSurprise}";
        }
    }

    [Category(CountStats), Description("启用后，在请求状态检查时输出累计统计信息。"), DisplayName("状态检查时输出计数")]
    public bool EmitCountsOnStatusCheck
    {
        get => CountStatsSettings.EmitCountsOnStatusCheck;
        set => CountStatsSettings.EmitCountsOnStatusCheck = value;
    }

    public IEnumerable<string> GetNonZeroCounts()
    {
        // Delegating the call to CountStatsSettingsCategory
        return CountStatsSettings.GetNonZeroCounts();
    }

    public class CategoryConverter<T> : TypeConverter
    {
        public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;

        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object value, Attribute[]? attributes) => TypeDescriptor.GetProperties(typeof(T));

        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType != typeof(string) && base.CanConvertTo(context, destinationType);
    }

    public enum ImageSize
    {
        Size256x256,

        Size128x128
    }

    public enum MoveType
    {
        Normal,
        Fighting,
        Flying,
        Poison,
        Ground,
        Rock,
        Bug,
        Ghost,
        Steel,
        Fire,
        Water,
        Grass,
        Electric,
        Psychic,
        Ice,
        Dragon,
        Dark,
        Fairy,
        Stellar
    }

    public class MoveTypeEmojiInfo
    {
        [Description("对应的招式属性。")]
        public MoveType MoveType { get; set; }
        [Description("该属性在 Discord 中使用的表情字符串。")]
        public string EmojiCode { get; set; } = string.Empty;
        public MoveTypeEmojiInfo()
        { }
        public MoveTypeEmojiInfo(MoveType moveType)
        {
            MoveType = moveType;
            EmojiCode = string.Empty;
        }
        public override string ToString()
        {
            if (string.IsNullOrEmpty(EmojiCode))
                return MoveType.ToString();
            return $"{EmojiCode}";
        }
    }

    public class TeraTypeEmojiInfo
    {
        [Description("对应的太晶属性。")]
        public MoveType MoveType { get; set; }
        [Description("该太晶属性在 Discord 中使用的表情字符串。")]
        public string EmojiCode { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public TeraTypeEmojiInfo()
        { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public TeraTypeEmojiInfo(MoveType teraType)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            MoveType = teraType;
        }
        public override string ToString()
        {
            if (string.IsNullOrEmpty(EmojiCode))
                return MoveType.ToString();
            return $"{EmojiCode}";
        }
    }
}
