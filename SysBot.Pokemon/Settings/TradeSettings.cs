using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class TradeSettings : IBotStateSettings, ICountSettings
{
    private const string CountStats = "统计信息";

    private const string HOMELegality = "HOME 合法性";

    private const string TradeConfig = "交易配置";

    private const string VGCPastesConfig = "VGC 导入";

    private const string Miscellaneous = "其他设置";

    private const string RequestFolders = "请求目录";

    private const string EmbedSettings = "嵌入显示";

    public override string ToString() => "交易配置";

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class EmojiInfo
    {
        [DisplayName("表情文本")]
        [Description("完整的表情文本，例如 <:custom:123456>.")]
        public string EmojiString { get; set; } = string.Empty;

        public override string ToString()
        {
            return string.IsNullOrEmpty(EmojiString) ? "未设置" : EmojiString;
        }
    }

    [Category(TradeConfig), Description("交易流程的通用配置。"), DisplayName("交易基础配置"), Browsable(true)]
    public TradeSettingsCategory TradeConfiguration { get; set; } = new();

    [Category(EmbedSettings), Description("Discord 中交易嵌入消息的显示设置。"), DisplayName("交易嵌入显示"), Browsable(true)]
    public TradeEmbedSettingsCategory TradeEmbedSettings { get; set; } = new();

    [Category(RequestFolders), Description("配置各类请求文件夹的路径。"), DisplayName("请求文件夹"), Browsable(true)]
    public RequestFolderSettingsCategory RequestFolderSettings { get; set; } = new();

    [Category(CountStats), Description("统计各类交易数量的相关设置。"), DisplayName("交易统计信息"), Browsable(true)]
    public CountStatsSettingsCategory CountStatsSettings { get; set; } = new();

    [Category(TradeConfig), TypeConverter(typeof(CategoryConverter<TradeSettingsCategory>))]
    public class TradeSettingsCategory
    {
        public override string ToString() => "交易基础配置";

        [Category(TradeConfig), Description("允许设置的最小联机密码。"), DisplayName("联机密码下限")]
        public int MinTradeCode { get; set; } = 0;

        [Category(TradeConfig), Description("允许设置的最大联机密码。"), DisplayName("联机密码上限")]
        public int MaxTradeCode { get; set; } = 9999_9999;

        [Category(TradeConfig), Description("启用后，将记住 Discord 用户的交易密码并重复使用。"), DisplayName("记住并复用交易密码")]
        public bool StoreTradeCodes { get; set; } = true;

        [Category(TradeConfig), Description("等待交易伙伴的时间（秒）。"), DisplayName("等待交易伙伴时间（秒）")]
        public int TradeWaitTime { get; set; } = 30;

        [Category(TradeConfig), Description("持续按 A 以等待交易处理的最长时间（秒）。"), DisplayName("确认交易最大等待时间（秒）")]
        public int MaxTradeConfirmTime { get; set; } = 25;

        [Category(TradeConfig), Description("为“物品交易”指定默认宝可梦物种。"), DisplayName("物品交易默认物种")]
        public Species ItemTradeSpecies { get; set; } = Species.None;

        [Category(TradeConfig), Description("当未指定物品时发送的默认携带物。"), DisplayName("默认携带物")]
        public HeldItem DefaultHeldItem { get; set; } = HeldItem.None;

        [Category(TradeConfig), Description("启用后，每只合法宝可梦默认携带推荐的可以重学的招式，无需额外批量指令。"), DisplayName("默认添加可重学招式")]
        public bool SuggestRelearnMoves { get; set; } = true;

        [Category(TradeConfig), Description("允许在一次会话中批量完成多个交易。"), DisplayName("允许批量交易")]
        public bool AllowBatchTrades { get; set; } = true;

        [Category(TradeConfig), Description("检查昵称与训练家名称是否存在垃圾信息。"), DisplayName("启用垃圾信息检测")]
        public bool EnableSpamCheck { get; set; } = true;

        [Category(TradeConfig), Description("单次交易可处理的宝可梦数量上限。设为 1 以下将关闭批量模式。"), DisplayName("单次交易宝可梦数量上限")]
        public int MaxPkmsPerTrade { get; set; } = 1;

        [Category(TradeConfig), Description("放生模式：单个用户的放生次数达到该值后停止。"), DisplayName("单次交易放生数量上限")]
        public int MaxDumpsPerTrade { get; set; } = 20;

        [Category(TradeConfig), Description("放生模式：在交易中累计等待达到该秒数后停止。"), DisplayName("放生交易最长持续时间（秒）")]
        public int MaxDumpTradeTime { get; set; } = 45;

        [Category(TradeConfig), Description("放生模式：启用后向用户输出合法性检查信息。"), DisplayName("放生模式合法性提示")]
        public bool DumpTradeLegalityCheck { get; set; } = true;

        [Category(TradeConfig), Description("适用于《Let's Go》系列的交易动画等待时间上限。")]
        public int TradeAnimationMaxDelaySeconds = 25;

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
        public override string ToString() => "交易嵌入显示设置";

        private bool _useEmbeds = true;

        [Category(EmbedSettings), Description("启用后，在 Discord 交易频道中展示图文嵌入；否则使用纯文本。"), DisplayName("启用嵌入消息")]
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

        [Category(EmbedSettings), Description("嵌入消息中宝可梦图片的默认尺寸。"), DisplayName("宝可梦图片尺寸")]
        public ImageSize PreferredImageSize { get; set; } = ImageSize.Size256x256;

        [Category(EmbedSettings), Description("在招式名称前显示对应属性表情（需在服务器上传表情）。"), DisplayName("显示招式属性表情")]
        public bool MoveTypeEmojis { get; set; } = true;

        [Category(EmbedSettings), Description("为各招式属性配置自定义表情。"), DisplayName("自定义属性表情")]
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

        [Category(EmbedSettings), Description("用于表示雄性宝可梦的表情文本。"), DisplayName("雄性表情")]
        public EmojiInfo MaleEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("用于表示雌性宝可梦的表情文本。"), DisplayName("雌性表情")]
        public EmojiInfo FemaleEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("用于显示神秘礼物状态的表情。"), DisplayName("神秘礼物表情")]
        public EmojiInfo MysteryGiftEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("用于展示 Alpha 标记的表情。"), DisplayName("Alpha 标记表情")]
        public EmojiInfo AlphaMarkEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("用于展示最强标记的表情。"), DisplayName("最强标记表情")]
        public EmojiInfo MightiestMarkEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("用于《阿尔宙斯》Alpha 状态的表情。"), DisplayName("PLA Alpha 表情")]
        public EmojiInfo AlphaPLAEmoji { get; set; } = new EmojiInfo();

        [Category(EmbedSettings), Description("在嵌入消息中显示太晶属性表情（需在服务器上传表情）。"), DisplayName("显示太晶属性表情")]
        public bool UseTeraEmojis { get; set; } = true;

        [Category(EmbedSettings), Description("为各太晶属性配置自定义表情。"), DisplayName("自定义太晶表情")]
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

        [Category(EmbedSettings), Description("在嵌入消息中显示体型信息（需上传表情，仅 SV）。"), DisplayName("显示体型信息")]
        public bool ShowScale { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入消息中显示太晶属性（仅 SV）。"), DisplayName("显示太晶属性")]
        public bool ShowTeraType { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入消息中显示等级。"), DisplayName("显示等级")]
        public bool ShowLevel { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入消息中显示遇见日期。"), DisplayName("显示遇见日期")]
        public bool ShowMetDate { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入消息中显示特性。"), DisplayName("显示特性")]
        public bool ShowAbility { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入消息中显示性格。"), DisplayName("显示性格")]
        public bool ShowNature { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入消息中显示宝可梦语言。"), DisplayName("显示语言")]
        public bool ShowLanguage { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入消息中显示个体值。"), DisplayName("显示个体值")]
        public bool ShowIVs { get; set; } = true;

        [Category(EmbedSettings), Description("在嵌入消息中显示努力值。"), DisplayName("显示努力值")]
        public bool ShowEVs { get; set; } = true;
    }

    [Category(RequestFolders), TypeConverter(typeof(CategoryConverter<RequestFolderSettingsCategory>))]
    public class RequestFolderSettingsCategory
    {
        public override string ToString() => "请求文件夹设置";

        [Category(RequestFolders), Description("事件（events）文件夹路径，请先创建“events”目录并填入完整路径。"), DisplayName("事件文件夹路径")]
        public string EventsFolder { get; set; } = string.Empty;

        [Category(RequestFolders), Description("对战配置（battleready）文件夹路径，请先创建“battleready”目录并填入完整路径。"), DisplayName("对战配置文件夹路径")]
        public string BattleReadyPKMFolder { get; set; } = string.Empty;
    }

    [Category(Miscellaneous)]
    [Description("交易期间关闭 Switch 屏幕显示。")]
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

        [DisplayName("惊喜交换次数")]
        [Category(CountStats), Description("已完成的惊喜交换次数。")]
        public int CompletedSurprise
        {
            get => _completedSurprise;
            set => _completedSurprise = value;
        }

        [DisplayName("分发交易次数")]
        [Category(CountStats), Description("已完成的分发类联机交易次数。")]
        public int CompletedDistribution
        {
            get => _completedDistribution;
            set => _completedDistribution = value;
        }

        [DisplayName("指定用户交易次数")]
        [Category(CountStats), Description("已完成的指定用户联机交易次数。")]
        public int CompletedTrades
        {
            get => _completedTrades;
            set => _completedTrades = value;
        }

        [DisplayName("修复 OT 交易次数")]
        [Category(CountStats), Description("已完成的修复 OT 交易次数。")]
        public int CompletedFixOTs
        {
            get => _completedFixOTs;
            set => _completedFixOTs = value;
        }

        [Browsable(false)]
        [DisplayName("种子检测次数")]
        [Category(CountStats), Description("已完成的种子检测交易次数。")]
        public int CompletedSeedChecks
        {
            get => _completedSeedChecks;
            set => _completedSeedChecks = value;
        }

        [DisplayName("克隆交易次数")]
        [Category(CountStats), Description("已完成的克隆交易次数。")]
        public int CompletedClones
        {
            get => _completedClones;
            set => _completedClones = value;
        }

        [DisplayName("放生交易次数")]
        [Category(CountStats), Description("已完成的放生交易次数。")]
        public int CompletedDumps
        {
            get => _completedDumps;
            set => _completedDumps = value;
        }

        [DisplayName("状态检查时输出统计")]
        [Category(CountStats), Description("启用后，在执行状态检查时输出上述统计。")]
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
                yield return $"种子检测交易：{CompletedSeedChecks}";
            if (CompletedClones != 0)
                yield return $"克隆交易：{CompletedClones}";
            if (CompletedDumps != 0)
                yield return $"放生交易：{CompletedDumps}";
            if (CompletedTrades != 0)
                yield return $"联机交易：{CompletedTrades}";
            if (CompletedDistribution != 0)
                yield return $"分发交易：{CompletedDistribution}";
            if (CompletedFixOTs != 0)
                yield return $"修复 OT 交易：{CompletedFixOTs}";
            if (CompletedSurprise != 0)
                yield return $"惊喜交换：{CompletedSurprise}";
        }
    }

    [Category(CountStats)]
    [DisplayName("状态检查时输出统计")]
    [Description("启用后，在执行状态检查时输出交易统计信息。")]
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
        [Description("用于该属性的 Discord 表情文本。")]
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
        [Description("用于该太晶属性的 Discord 表情文本。")]
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
