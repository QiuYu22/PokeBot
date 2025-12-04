using System;
using System.Collections.Generic;

namespace SysBot.Pokemon;

/// <summary>
/// 剑/盾 RAM 偏移
/// </summary>
public class PokeDataOffsetsSWSH
{
    public const int BoxFormatSlotSize = 0x158;

    public const uint BoxStartOffset = 0x45075880;

    public const uint CurrentBoxOffset = 0x450C680E;

    public const uint DayCare_Route5_Egg_Is_Ready = 0x4511F9A8;

    public const uint DayCare_Route5_Step_Counter = 0x4511F99C;

    public const uint InBattleRaidOffsetSH = 0x3F128626;

    // 不在战斗或极巨团战时为 0，否则为 0x40 或 0x41。
    public const uint InBattleRaidOffsetSW = 0x3F128624;

    public const uint IsConnectedOffset = 0x30c7cca8;

    public const uint ItemTreasureAddress = 0x45068970;

    public const uint LegendaryPokemonOffset = 0x886BC348;

    public const uint LinkTradePartnerNameOffset = 0xAF28384C;

    public const uint LinkTradePartnerNIDOffset = 0xAF2846B0;

    // Link Trade Offsets
    public const uint LinkTradePartnerPokemonOffset = 0xAF286078;

    public const uint LinkTradePartnerTIDSIDOffset = LinkTradePartnerNameOffset - 0x8;

    public const uint LinkTradeSearchingOffset = 0x2F76C3C8;

    // 需要加到每只宝可梦偏移上的值，使用 AltForm。
    public const uint RaidAltFormInc = 0x4;

    public const uint RaidBossOffset = 0x8398A25C;

    // 需要加到每只宝可梦偏移上的值：0=雄性，1=雌性，2=无性别。
    public const uint RaidGenderIncr = 0x8;

    // 需要加到每只宝可梦偏移上的值，用于表示玩家是否已锁定宝可梦。
    public const uint RaidLockedInIncr = 0x1C;

    // 团战偏移
    // 房主当前选择的宝可梦图鉴编号。
    // 每位玩家详情占 0x30，逐次加 0x30 即可取得下一位玩家。
    public const uint RaidP0PokemonOffset = 0x8398A294;

    public const uint RaidPokemonOffset = 0x886A95B8;

    // 需要加到每只宝可梦偏移上的值，表示该宝可梦是否为闪光。
    public const uint RaidShinyIncr = 0xC;

    public const string ShieldID = "01008DB008C2C000";

    public const uint SoftBanUnixTimespanOffset = 0x450C89E8;

    public const uint SurpriseTradeLockBox = 0x450676f8;

    public const uint SurpriseTradeLockSlot = 0x450676fc;

    public const uint SurpriseTradePartnerNameOffset = 0x45067708;

    // Surprise Trade Offsets
    public const uint SurpriseTradePartnerPokemonOffset = 0x450675a0;

    public const uint SurpriseTradePartnerTIDSIDOffset = SurpriseTradePartnerNameOffset - 0x8;

    public const uint SurpriseTradeSearch_Empty = 0x00000000;

    public const uint SurpriseTradeSearch_Found = 0x0200012C;

    public const uint SurpriseTradeSearch_Searching = 0x01000000;

    public const uint SurpriseTradeSearchOffset = 0x45067704;

    public const string SwordID = "0100ABF008968000";

    public const string SWSHGameVersion = "1.3.2";

    public const uint TextSpeedOffset = 0x450690A0;

    public const int TrainerDataLength = 0x110;

    public const uint TrainerDataOffset = 0x45068F18;

    // Pokémon Encounter Offsets
    public const uint WildPokemonOffset = 0x8FEA3648;

    /* 5 号道路培育屋 */

    #region ScreenDetection

    // 用于检测当前是否在战斗菜单，以便可以逃跑。
    public const uint BattleMenuOffset = 0x6B578EDC;

    // 用于检测是否处于盒子界面，不同用户可能会读到任一数值。
    public const uint CurrentScreen_Box1 = 0xFF00D59B;

    public const uint CurrentScreen_Box2 = 0xFF000000;

    // 用户被软封禁时的值。
    public const uint CurrentScreen_Softban = 0xFF000000;

    // 原始的屏幕检测偏移。
    public const uint CurrentScreenOffset = 0x6B30FA00;

    // 稳定的场景检测，处于场景时为 1，反之为 0。
    public IReadOnlyList<long> OverworldPointer { get; } = [0x2636678, 0xC0, 0x80];

    #endregion ScreenDetection

    public static uint GetTrainerNameOffset(TradeMethod tradeMethod) => tradeMethod switch
    {
        TradeMethod.LinkTrade => LinkTradePartnerNameOffset,
        TradeMethod.SurpriseTrade => SurpriseTradePartnerNameOffset,
        _ => throw new ArgumentException("该交易方式不支持训练家名称偏移。", nameof(tradeMethod)),
    };

    public static uint GetTrainerTIDSIDOffset(TradeMethod tradeMethod) => tradeMethod switch
    {
        TradeMethod.LinkTrade => LinkTradePartnerTIDSIDOffset,
        TradeMethod.SurpriseTrade => SurpriseTradePartnerTIDSIDOffset,
        _ => throw new ArgumentException("该交易方式不支持训练家 TID/SID 偏移。", nameof(tradeMethod)),
    };
}
