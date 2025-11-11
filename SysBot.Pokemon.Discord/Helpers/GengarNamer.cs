using PKHeX.Core;

namespace SysBot.Pokemon.Discord;

public sealed class GengarNamer : IFileNamer<PKM>
{
    public string Name => "默认";

    public string GetName(PKM obj)
    {
        if (obj is GBPKM gb)
            return GetGBPKM(gb);
        return GetRegular(obj);
    }

    private static string GetAbility(PKM pk)
    {
        int abilityIndex = pk.Ability;

        // You need to implement a method similar to Util.GetNaturesList for abilities
        var abilityStrings = Util.GetAbilitiesList("zh");
        if ((uint)abilityIndex >= abilityStrings.Length)
            abilityIndex = 0;
        return abilityStrings[abilityIndex];
    }

    private static string GetConditionalTeraType(PKM pk)
    {
        if (pk is not ITeraType t)
            return string.Empty;
        var type = t.GetTeraType();
        var type_str = ((byte)type == TeraTypeUtil.Stellar) ? "全能" : type.ToString();
        return $"太晶（{type_str}）";
    }

    private static string GetGBPKM(GBPKM gb)
    {
        string form = gb.Form > 0 ? $"-{gb.Form:00}" : string.Empty;
        string star = gb.IsShiny ? " ★" : string.Empty;
        int metYear = gb.MetYear;
        string metYearString = metYear > 0 ? $"-{metYear + 2000}" : string.Empty;
        string IVList = $"{gb.IV_HP}.{gb.IV_ATK}.{gb.IV_DEF}.{gb.IV_SPA}.{gb.IV_SPD}.{gb.IV_SPE}";
        string speciesName = SpeciesName.GetSpeciesNameGeneration(gb.Species, (int)LanguageID.ChineseS, gb.Format);
        return $"{speciesName} - {gb.Species:000}{form}{star} - {IVList} - {metYearString}";
    }

    private static string GetNature(PKM pk)
    {
        var nature = pk.Nature;
        var strings = Util.GetNaturesList("zh");
        if ((uint)nature >= strings.Length)
            nature = 0;
        return strings[(uint)nature];
    }

    private static string GetRegular(PKM pk)
    {
        string form = pk.Form > 0 ? $"-{pk.Form:00}" : string.Empty;
        string shinytype = GetShinyTypeString(pk);

        string IVList = $"{pk.IV_HP}.{pk.IV_ATK}.{pk.IV_DEF}.{pk.IV_SPA}.{pk.IV_SPD}.{pk.IV_SPE}";

        int metYear = pk.MetYear;
        string metYearString = metYear > 0 ? $"{metYear + 2000}" : string.Empty;

        string speciesName = SpeciesName.GetSpeciesNameGeneration(pk.Species, (int)LanguageID.ChineseS, pk.Format);
        if (pk is IGigantamax { CanGigantamax: true })
            speciesName += "-Gmax";

        return $"{speciesName}{shinytype}-{GetConditionalTeraType(pk)}-{GetNature(pk)}-{GetAbility(pk)}-{IVList}-{metYearString}-{GetVersion(pk)}";
    }

    private static string GetShinyTypeString(PKM pk)
    {
        if (!pk.IsShiny)
            return string.Empty;
        if (pk.Format >= 8 && (pk.ShinyXor == 0 || pk.FatefulEncounter || pk.Version == GameVersion.GO))
            return " ■";
        return " ★";
    }

    private static string GetVersion(PKM pk)
    {
        if (pk.E) return "绿宝石";
        if (pk.FRLG) return "火红/叶绿";
        if (pk.Pt) return "白金";
        if (pk.HGSS) return "心金/魂银";
        if (pk.BW) return "黑/白";
        if (pk.B2W2) return "黑2/白2";
        if (pk.XY) return "X/Y";
        if (pk.AO) return "终极红宝石/始源蓝宝石";
        if (pk.SM) return "太阳/月亮";
        if (pk.USUM) return "究极之日/究极之月";
        if (pk.GO) return "Pokémon GO";
        if (pk.VC1) return "VC 红/绿/蓝/皮卡丘";
        if (pk.VC2) return "VC 金/银/水晶";
        if (pk.LGPE) return "Let's Go 皮卡丘/伊布";
        if (pk.SWSH) return "剑/盾";
        if (pk.BDSP) return "晶灿钻石/明亮珍珠";
        if (pk.LA) return "阿尔宙斯";
        if (pk.SV) return "朱/紫";
        return "未知版本";
    }
}
