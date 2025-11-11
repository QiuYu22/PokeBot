using System;
using FluentAssertions;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using SysBot.Pokemon;
using Xunit;

namespace SysBot.Tests;

/// <summary>
/// 使用 ALM 的 GenerateEgg 方法验证蛋生成逻辑是否正确。
/// </summary>
public class EggTests
{
    static EggTests() => AutoLegalityWrapper.EnsureInitialized(new Pokemon.LegalitySettings());

    [Theory]
    [InlineData("Sprigatito", "Protean", true, false)]
    [InlineData("Fuecoco", "Unaware", false, true)]
    [InlineData("Quaxly", "Moxie", true, false)]
    public void CanGenerateGen9Eggs(string species, string ability, bool isMale, bool isShiny)
    {
        // 准备阶段
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        var set = new ShowdownSet($@"Egg ({species}) ({(isMale ? "M" : "F")})
Ability: {ability}
Ball: Poke Ball
Shiny: {(isShiny ? "Yes" : "No")}");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // 执行阶段
        var egg = sav.GenerateEgg(template, out var result);

        // 断言阶段
        result.Should().Be(LegalizationResult.Regenerated, "蛋生成过程应该成功");
        egg.Should().NotBeNull();
        egg.Should().BeOfType<PK9>();

        var pk9 = (PK9)egg;
        pk9.IsEgg.Should().BeTrue("生成的宝可梦应为蛋状态");
        pk9.Species.Should().Be((ushort)Enum.Parse<Species>(species));
        pk9.EggLocation.Should().Be(Locations.Picnic9, "第九世代的蛋来源于野餐");
        pk9.Version.Should().Be(0, "未孵化的第九世代蛋版本号为 0");
        pk9.IsShiny.Should().Be(isShiny);

        var la = new LegalityAnalysis(pk9);
        la.Valid.Should().BeTrue($"蛋数据应合法:\n{la.Report()}");
    }

    [Theory]
    [InlineData("Grookey", "Grassy Surge", true, false)]
    [InlineData("Scorbunny", "Libero", true, true)]
    [InlineData("Sobble", "Torrent", false, true)]
    public void CanGenerateGen8Eggs(string species, string ability, bool isMale, bool isShiny)
    {
        // 准备阶段
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK8>();
        var set = new ShowdownSet($@"Egg ({species}) ({(isMale ? "M" : "F")})
Ability: {ability}
Ball: Poke Ball
Shiny: {(isShiny ? "Yes" : "No")}");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // 执行阶段
        var egg = sav.GenerateEgg(template, out var result);

        // 断言阶段
        result.Should().Be(LegalizationResult.Regenerated, "蛋生成过程应该成功");
        egg.Should().NotBeNull();
        egg.Should().BeOfType<PK8>();

        var pk8 = (PK8)egg;
        pk8.IsEgg.Should().BeTrue("生成的宝可梦应为蛋状态");
        pk8.Species.Should().Be((ushort)Enum.Parse<Species>(species));
        pk8.IsShiny.Should().Be(isShiny);

        var la = new LegalityAnalysis(pk8);
        la.Valid.Should().BeTrue($"蛋数据应合法:\n{la.Report()}");
    }

    [Theory]
    [InlineData("Turtwig", "Shell Armor", true, false)]
    [InlineData("Chimchar", "Iron Fist", true, true)]
    [InlineData("Piplup", "Competitive", false, false)]
    public void CanGenerateBDSPEggs(string species, string ability, bool isMale, bool isShiny)
    {
        // 准备阶段
        var sav = AutoLegalityWrapper.GetTrainerInfo<PB8>();
        var set = new ShowdownSet($@"Egg ({species}) ({(isMale ? "M" : "F")})
Ability: {ability}
Ball: Poke Ball
Shiny: {(isShiny ? "Yes" : "No")}");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // 执行阶段
        var egg = sav.GenerateEgg(template, out var result);

        // 断言阶段
        result.Should().Be(LegalizationResult.Regenerated, "蛋生成过程应该成功");
        egg.Should().NotBeNull();
        egg.Should().BeOfType<PB8>();

        var pb8 = (PB8)egg;
        pb8.IsEgg.Should().BeTrue("生成的宝可梦应为蛋状态");
        pb8.Species.Should().Be((ushort)Enum.Parse<Species>(species));
        pb8.IsShiny.Should().Be(isShiny);

        var la = new LegalityAnalysis(pb8);
        la.Valid.Should().BeTrue($"蛋数据应合法:\n{la.Report()}");
    }

    [Fact]
    public void EggShouldHaveCorrectFriendship()
    {
        // 准备阶段
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        var set = new ShowdownSet(@"Egg (Sprigatito) (M)
Ability: Overgrow
Ball: Poke Ball");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // 执行阶段
        var egg = sav.GenerateEgg(template, out var result);

        // 断言阶段
        result.Should().Be(LegalizationResult.Regenerated);
        egg.Should().NotBeNull();

        var pk9 = (PK9)egg;
        pk9.OriginalTrainerFriendship.Should().BeGreaterOrEqualTo(1, "蛋必须至少保留最小孵化周期的亲密度");
        pk9.OriginalTrainerFriendship.Should().BeLessOrEqualTo(pk9.PersonalInfo.HatchCycles, "蛋亲密度不应超过物种的孵化周期");
    }

    [Fact]
    public void EggShouldHaveEggMoves()
    {
        // 准备阶段
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        var set = new ShowdownSet(@"Egg (Sprigatito) (M)
Ability: Overgrow
Ball: Poke Ball
- Scratch
- Tail Whip
- Leafage");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // 执行阶段
        var egg = sav.GenerateEgg(template, out var result);

        // 断言阶段
        result.Should().Be(LegalizationResult.Regenerated);
        egg.Should().NotBeNull();

        var pk9 = (PK9)egg;
        pk9.Move1.Should().NotBe(0, "蛋至少应保留一项技能");

        var la = new LegalityAnalysis(pk9);
        la.Valid.Should().BeTrue($"带技能的蛋应合法:\n{la.Report()}");
    }

    [Fact]
    public void EggNicknameShouldBeCorrect()
    {
        // 准备阶段
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        var set = new ShowdownSet(@"Egg (Sprigatito) (M)
Ability: Overgrow
Ball: Poke Ball");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // 执行阶段
        var egg = sav.GenerateEgg(template, out var result);

        // 断言阶段
        result.Should().Be(LegalizationResult.Regenerated);
        egg.Should().NotBeNull();

        var pk9 = (PK9)egg;
        pk9.Nickname.Should().Be("Egg", "未孵化的蛋在英文版中昵称应为“Egg”");
        pk9.IsNicknamed.Should().BeTrue("蛋需要设置昵称标志");
    }
}
