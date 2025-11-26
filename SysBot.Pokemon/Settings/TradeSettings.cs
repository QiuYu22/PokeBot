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

    private const string HOMELegality = "HOME合法性";

    private const string TradeConfig = "交易配置";

    private const string VGCPastesConfig = "VGC配置";

    private const string Miscellaneous = "杂项";

    private const string RequestFolders = "请求文件夹";

    private const string EmbedSettings = "嵌入设置";

    public override string ToString() => "交易配置设置";

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class EmojiInfo
    {
        [Description("表情符号的完整字符串。"), DisplayName("表情符号字符串")]
        public string EmojiString { get; set; } = string.Empty;

        public override string ToString()
        {
            return string.IsNullOrEmpty(EmojiString) ? "未设置" : EmojiString;
        }
    }

    [Category(TradeConfig), Description("与交易配置相关的设置。"), DisplayName("交易配置"), Browsable(true)]
    public TradeSettingsCategory TradeConfiguration { get; set; } = new();

    [Category(EmbedSettings), Description("与 Discord 交易嵌入相关的设置。"), DisplayName("交易嵌入设置"), Browsable(true)]
    public TradeEmbedSettingsCategory TradeEmbedSettings { get; set; } = new();

    [Category(RequestFolders), Description("与请求文件夹相关的设置。"), DisplayName("请求文件夹设置"), Browsable(true)]
    public RequestFolderSettingsCategory RequestFolderSettings { get; set; } = new();

    [Category(CountStats), Description("与交易统计相关的设置。"), DisplayName("交易统计设置"), Browsable(true)]
    public CountStatsSettingsCategory CountStatsSettings { get; set; } = new();

    [Category(TradeConfig), TypeConverter(typeof(CategoryConverter<TradeSettingsCategory>))]
    public class TradeSettingsCategory
    {
        public override string ToString() => "交易配置设置";

        [Category(TradeConfig), Description("最小连接密码。"), DisplayName("最小交易连接密码")]
        public int MinTradeCode { get; set; } = 0;

        [Category(TradeConfig), Description("最大连接密码。"), DisplayName("最大交易连接密码")]
        public int MaxTradeCode { get; set; } = 9999_9999;

        [Category(TradeConfig), Description("设置为 True 时，Discord 用户的交易密码将被存储并重复使用而不更改。"), DisplayName("存储并重用交易密码")]
        public bool StoreTradeCodes { get; set; } = true;

        [Category(TradeConfig), Description("等待交易伙伴的时间（秒）。"), DisplayName("交易伙伴等待时间（秒）")]
        public int TradeWaitTime { get; set; } = 30;

        [Category(TradeConfig), Description("按 A 等待交易处理的最长时间（秒）。"), DisplayName("最大交易确认时间（秒）")]
        public int MaxTradeConfirmTime { get; set; } = 25;

        [Category(TradeConfig), Description("为\"道具交易\"选择默认物种（如已配置）。"), DisplayName("道具交易默认物种")]
        public Species ItemTradeSpecies { get; set; } = Species.None;

        [Category(TradeConfig), Description("如果未指定，则发送的默认持有物品。"), DisplayName("交易默认持有物品")]
        public HeldItem DefaultHeldItem { get; set; } = HeldItem.None;

        [Category(TradeConfig), Description("设置为 True 时，每只有效的宝可梦都将附带所有建议的可回忆招式，无需批处理命令。"), DisplayName("默认建议可回忆招式")]
        public bool SuggestRelearnMoves { get; set; } = true;

        [Category(TradeConfig), Description("切换以允许或禁止批量交易。"), DisplayName("允许批量交易")]
        public bool AllowBatchTrades { get; set; } = true;

        [Category(TradeConfig), Description("检查昵称和原训练家是否为垃圾信息。"), DisplayName("启用垃圾信息检查")]
        public bool EnableSpamCheck { get; set; } = true;

        [Category(TradeConfig), Description("单次交易的最大宝可梦数量。如果此配置小于 1，批量模式将关闭。"), DisplayName("每次交易最大宝可梦数")]
        public int MaxPkmsPerTrade { get; set; } = 1;

        [Category(TradeConfig), Description("转储交易：单个用户的转储程序在达到最大转储次数后将停止。"), DisplayName("每次交易最大转储数")]
        public int MaxDumpsPerTrade { get; set; } = 20;

        [Category(TradeConfig), Description("转储交易：转储程序在交易中花费 x 秒后将停止。"), DisplayName("最大转储交易时间（秒）")]
        public int MaxDumpTradeTime { get; set; } = 45;

        [Category(TradeConfig), Description("转储交易：如果启用，转储程序将向用户输出合法性检查信息。"), DisplayName("转储交易合法性检查")]
        public bool DumpTradeLegalityCheck { get; set; } = true;

        [Category(TradeConfig), Description("启用后，如果提供的宝可梦会进化，机器人将自动取消交易。"), DisplayName("禁止交易进化")]
        public bool DisallowTradeEvolve { get; set; } = true;

        [Category(TradeConfig), Description("LGPE 设置。"), DisplayName("交易动画最大延迟秒数")]
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
        public override string ToString() => "交易嵌入配置设置";

        private bool _useEmbeds = true;

        [Category(EmbedSettings), Description("如果为 true，将在你的 Discord 交易频道中显示漂亮的嵌入来展示用户正在交易的内容。False 将显示默认文本。"), DisplayName("使用嵌入")]
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

        [Category(EmbedSettings), Description("嵌入中首选的物种图片大小。"), DisplayName("物种图片大小")]
        public ImageSize PreferredImageSize { get; set; } = ImageSize.Size256x256;

        [Category(EmbedSettings), Description("在交易嵌入中的招式旁边显示招式类型图标（仅限 Discord）。需要用户将表情符号上传到他们的服务器。"), DisplayName("显示招式类型表情")]
        public bool MoveTypeEmojis { get; set; } = true;

        [Category(EmbedSettings), Description("招式类型的自定义表情符号信息。"), DisplayName("自定义类型表情")]
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

        [Category(EmbedSettings), Description("雄性性别表情符号的完整字符串。"), DisplayName("雄性表情")]
        public EmojiInfo MaleEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("雌性性别表情符号的完整字符串。"), DisplayName("雌性表情")]
        public EmojiInfo FemaleEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("显示神秘礼物状态的表情符号信息。"), DisplayName("神秘礼物表情")]
        public EmojiInfo MysteryGiftEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("显示阿尔法标记的表情符号信息。"), DisplayName("阿尔法标记表情")]
        public EmojiInfo AlphaMarkEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("显示最强标记的表情符号信息。"), DisplayName("最强标记表情")]
        public EmojiInfo MightiestMarkEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("在传说阿尔宙斯中显示阿尔法表情符号的信息。"), DisplayName("阿尔法 PLA 表情")]
        public EmojiInfo AlphaPLAEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("在交易嵌入中的招式旁边显示招式类型图标（仅限 Discord）。需要用户将表情符号上传到他们的服务器。"), DisplayName("显示太晶属性表情？")]
        public bool UseTeraEmojis { get; set; } = true;

        [Category(EmbedSettings), Description("太晶属性的表情符号信息。"), DisplayName("自定义太晶属性表情")]
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

        [Category(EmbedSettings), Description("在交易嵌入中显示体型（仅限 SV 和 Discord）。需要用户将表情符号上传到他们的服务器。"), DisplayName("显示体型")]
        public bool ShowScale { get; set; } = true;

        [Category(EmbedSettings), Description("在交易嵌入中显示太晶属性（仅限 SV 和 Discord）。"), DisplayName("显示太晶属性")]
        public bool ShowTeraType { get; set; } = true;

        [Category(EmbedSettings), Description("在交易嵌入中显示等级（仅限 Discord）。"), DisplayName("显示等级")]
        public bool ShowLevel { get; set; } = true;

        [Category(EmbedSettings), Description("在交易嵌入中显示相遇日期（仅限 Discord）。"), DisplayName("显示相遇日期")]
        public bool ShowMetDate { get; set; } = true;

        [Category(EmbedSettings), Description("在交易嵌入中显示特性（仅限 Discord）。"), DisplayName("显示特性")]
        public bool ShowAbility { get; set; } = true;

        [Category(EmbedSettings), Description("在交易嵌入中显示性格（仅限 Discord）。"), DisplayName("显示性格")]
        public bool ShowNature { get; set; } = true;

        [Category(EmbedSettings), Description("在交易嵌入中显示宝可梦语言（仅限 Discord）。"), DisplayName("显示语言")]
        public bool ShowLanguage { get; set; } = true;

        [Category(EmbedSettings), Description("在交易嵌入中显示个体值（仅限 Discord）。"), DisplayName("显示个体值")]
        public bool ShowIVs { get; set; } = true;

        [Category(EmbedSettings), Description("在交易嵌入中显示努力值（仅限 Discord）。"), DisplayName("显示努力值")]
        public bool ShowEVs { get; set; } = true;
    }

    [Category(RequestFolders), TypeConverter(typeof(CategoryConverter<RequestFolderSettingsCategory>))]
    public class RequestFolderSettingsCategory
    {
        public override string ToString() => "请求文件夹设置";

        [Category("请求文件夹"), Description("你的活动文件夹路径。创建一个名为 'events' 的新文件夹并在此处粘贴路径。"), DisplayName("活动文件夹路径")]
        public string EventsFolder { get; set; } = string.Empty;

        [Category("请求文件夹"), Description("你的对战就绪文件夹路径。创建一个名为 'battleready' 的新文件夹并在此处粘贴路径。"), DisplayName("对战就绪文件夹路径")]
        public string BattleReadyPKMFolder { get; set; } = string.Empty;
    }

    [Category(Miscellaneous), Description("在交易期间关闭 Switch 的屏幕"), DisplayName("关闭屏幕")]
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

        [Category(CountStats), Description("已完成的魔法交换次数"), DisplayName("已完成魔法交换")]
        public int CompletedSurprise
        {
            get => _completedSurprise;
            set => _completedSurprise = value;
        }

        [Category(), Description("已完成的连接交易次数（分发）"), DisplayName("已完成分发")]
        public int CompletedDistribution
        {
            get => _completedDistribution;
            set => _completedDistribution = value;
        }

        [Category(CountStats), Description("已完成的连接交易次数（指定用户）"), DisplayName("已完成交易")]
        public int CompletedTrades
        {
            get => _completedTrades;
            set => _completedTrades = value;
        }

        [Category(CountStats), Description("已完成的 FixOT 交易次数（指定用户）"), DisplayName("已完成 FixOT")]
        public int CompletedFixOTs
        {
            get => _completedFixOTs;
            set => _completedFixOTs = value;
        }

        [Browsable(false)]
        [Category(CountStats), Description("已完成的种子检查交易次数"), DisplayName("已完成种子检查")]
        public int CompletedSeedChecks
        {
            get => _completedSeedChecks;
            set => _completedSeedChecks = value;
        }

        [Category(CountStats), Description("已完成的克隆交易次数（指定用户）"), DisplayName("已完成克隆")]
        public int CompletedClones
        {
            get => _completedClones;
            set => _completedClones = value;
        }

        [Category(CountStats), Description("已完成的转储交易次数（指定用户）"), DisplayName("已完成转储")]
        public int CompletedDumps
        {
            get => _completedDumps;
            set => _completedDumps = value;
        }

        [Category(CountStats), Description("启用后，在请求状态检查时将输出统计数据。"), DisplayName("状态检查时输出统计")]
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
                yield return $"种子检测交易: {CompletedSeedChecks}";
            if (CompletedClones != 0)
                yield return $"克隆交易: {CompletedClones}";
            if (CompletedDumps != 0)
                yield return $"导出交易: {CompletedDumps}";
            if (CompletedTrades != 0)
                yield return $"连接交易: {CompletedTrades}";
            if (CompletedDistribution != 0)
                yield return $"分发交易: {CompletedDistribution}";
            if (CompletedFixOTs != 0)
                yield return $"修复OT交易: {CompletedFixOTs}";
            if (CompletedSurprise != 0)
                yield return $"魔法交换: {CompletedSurprise}";
        }
    }

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
        [Description("招式的属性。"), DisplayName("招式属性")]
        public MoveType MoveType { get; set; }
        [Description("此招式属性的 Discord 表情符号字符串。"), DisplayName("表情符号代码")]
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
        [Description("太晶属性。"), DisplayName("太晶属性")]
        public MoveType MoveType { get; set; }
        [Description("此太晶属性的 Discord 表情符号字符串。"), DisplayName("表情符号代码")]
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
