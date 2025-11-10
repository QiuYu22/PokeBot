using FluentAssertions;
using PKHeX.Core;
using SysBot.Pokemon;
using Xunit;

namespace SysBot.Tests;

public class GenerateTests
{
    // 勇士雄鹰对战配置（♀，大师球）
    private const string Braviary =
        @"Braviary (F) @ Master Ball
Ability: Defiant
EVs: 252 Atk / 4 SpD / 252 Spe
Jolly Nature
- Brave Bird
- Close Combat
- Tailwind
- Iron Head";

    // 喷火龙对战配置（选择围巾，太阳之力）
    private const string Charizard4 =
        @"Charizard @ Choice Scarf
Ability: Solar Power
Level: 50
Shiny: Yes
EVs: 252 SpA / 4 SpD / 252 Spe
Timid Nature
- Heat Wave
- Air Slash
- Solar Beam
- Beat Up";

    // 巨龟兽极巨配置（龙化石）
    private const string Drednaw =
        @"Drednaw-Gmax @ Fossilized Drake
Ability: Shell Armor
Level: 60
EVs: 252 Atk / 4 SpD / 252 Spe
Adamant Nature
- Earthquake
- Liquidation
- Swords Dance
- Head Smash";

    // 耿鬼极巨配置（命玉）
    private const string Gengar =
        @"Gengar-Gmax @ Life Orb
Ability: Cursed Body
Shiny: Yes
EVs: 252 SpA / 4 SpD / 252 Spe
Timid Nature
- Dream Eater
- Fling
- Giga Impact
- Headbutt";

    // 非法配置：缺少 Showdown 格式要求的内容，只提供了种族名
    private const string InvalidSpec = "(Pikachu)";

    // 坦克顶配：煤炭龟对战配置（突击背心）
    private const string Torkoal2 =
        @"Torkoal (M) @ Assault Vest
IVs: 0 Atk
EVs: 248 HP / 8 Atk / 252 SpA
Ability: Drought
Quiet Nature
- Body Press
- Earth Power
- Eruption
- Fire Blast";

    static GenerateTests() => AutoLegalityWrapper.EnsureInitialized(new Pokemon.LegalitySettings());

    [Theory]
    [InlineData(Gengar)]
    [InlineData(Braviary)]
    [InlineData(Drednaw)]
    public void CanGenerate(string set)
    {
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK8>();
        var s = new ShowdownSet(set);
        var template = AutoLegalityWrapper.GetTemplate(s);
        var pk = sav.GetLegal(template, out _);
        pk.Should().NotBeNull();
    }

    [Theory]
    [InlineData(InvalidSpec)]
    public void ShouldNotGenerate(string set)
    {
        _ = AutoLegalityWrapper.GetTrainerInfo<PK8>();
        var s = ShowdownUtil.ConvertToShowdown(set);
        s.Should().BeNull();
    }

    [Theory]
    [InlineData(Torkoal2, 2)]
    [InlineData(Charizard4, 4)]
    public void TestAbility(string set, int abilNumber)
    {
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK8>();
        for (int i = 0; i < 10; i++)
        {
            var s = new ShowdownSet(set);
            var template = AutoLegalityWrapper.GetTemplate(s);
            var pk = sav.GetLegal(template, out _);
            pk.AbilityNumber.Should().Be(abilNumber);
        }
    }

    [Theory]
    [InlineData(Torkoal2, 2)]
    [InlineData(Charizard4, 4)]
    public void TestAbilityTwitch(string set, int abilNumber)
    {
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK8>();
        for (int i = 0; i < 10; i++)
        {
            var twitch = set.Replace("\r\n", " ").Replace("\n", " ");
            var s = ShowdownUtil.ConvertToShowdown(twitch);
            var template = s == null ? null : AutoLegalityWrapper.GetTemplate(s);
            var pk = template == null ? null : sav.GetLegal(template, out _);
            pk.Should().NotBeNull();
            pk!.AbilityNumber.Should().Be(abilNumber);
        }
    }
}
