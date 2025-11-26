using System;
using FluentAssertions;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using SysBot.Pokemon;
using Xunit;

namespace SysBot.Tests;

/// <summary>
/// Tests to verify egg generation works correctly using ALM's GenerateEgg method.
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
        // Arrange
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        var set = new ShowdownSet($@"Egg ({species}) ({(isMale ? "M" : "F")})
Ability: {ability}
Ball: Poke Ball
Shiny: {(isShiny ? "Yes" : "No")}");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // Act
        var egg = sav.GenerateEgg(template, out var result);

        // Assert
        result.Should().Be(LegalizationResult.Regenerated, "蛋生成应该成功");
        egg.Should().NotBeNull();
        egg.Should().BeOfType<PK9>();

        var pk9 = (PK9)egg;
        pk9.IsEgg.Should().BeTrue("该宝可梦应该是一颗蛋");
        pk9.Species.Should().Be((ushort)Enum.Parse<Species>(species));
        pk9.EggLocation.Should().Be(Locations.Picnic9, "第九世代的蛋来自野餐");
        pk9.Version.Should().Be(0, "未孵化的第九世代蛋的版本应该为 0");
        pk9.IsShiny.Should().Be(isShiny);

        var la = new LegalityAnalysis(pk9);
        la.Valid.Should().BeTrue($"该蛋应该是合法的:\n{la.Report()}");
    }

    [Theory]
    [InlineData("Grookey", "Grassy Surge", true, false)]
    [InlineData("Scorbunny", "Libero", true, true)]
    [InlineData("Sobble", "Torrent", false, true)]
    public void CanGenerateGen8Eggs(string species, string ability, bool isMale, bool isShiny)
    {
        // Arrange
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK8>();
        var set = new ShowdownSet($@"Egg ({species}) ({(isMale ? "M" : "F")})
Ability: {ability}
Ball: Poke Ball
Shiny: {(isShiny ? "Yes" : "No")}");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // Act
        var egg = sav.GenerateEgg(template, out var result);

        // Assert
        result.Should().Be(LegalizationResult.Regenerated, "蛋生成应该成功");
        egg.Should().NotBeNull();
        egg.Should().BeOfType<PK8>();

        var pk8 = (PK8)egg;
        pk8.IsEgg.Should().BeTrue("该宝可梦应该是一颗蛋");
        pk8.Species.Should().Be((ushort)Enum.Parse<Species>(species));
        pk8.IsShiny.Should().Be(isShiny);

        var la = new LegalityAnalysis(pk8);
        la.Valid.Should().BeTrue($"该蛋应该是合法的:\n{la.Report()}");
    }

    [Theory]
    [InlineData("Turtwig", "Shell Armor", true, false)]
    [InlineData("Chimchar", "Iron Fist", true, true)]
    [InlineData("Piplup", "Competitive", false, false)]
    public void CanGenerateBDSPEggs(string species, string ability, bool isMale, bool isShiny)
    {
        // Arrange
        var sav = AutoLegalityWrapper.GetTrainerInfo<PB8>();
        var set = new ShowdownSet($@"Egg ({species}) ({(isMale ? "M" : "F")})
Ability: {ability}
Ball: Poke Ball
Shiny: {(isShiny ? "Yes" : "No")}");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // Act
        var egg = sav.GenerateEgg(template, out var result);

        // Assert
        result.Should().Be(LegalizationResult.Regenerated, "蛋生成应该成功");
        egg.Should().NotBeNull();
        egg.Should().BeOfType<PB8>();

        var pb8 = (PB8)egg;
        pb8.IsEgg.Should().BeTrue("该宝可梦应该是一颗蛋");
        pb8.Species.Should().Be((ushort)Enum.Parse<Species>(species));
        pb8.IsShiny.Should().Be(isShiny);

        var la = new LegalityAnalysis(pb8);
        la.Valid.Should().BeTrue($"该蛋应该是合法的:\n{la.Report()}");
    }

    [Fact]
    public void EggShouldHaveCorrectFriendship()
    {
        // Arrange
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        var set = new ShowdownSet(@"Egg (Sprigatito) (M)
Ability: Overgrow
Ball: Poke Ball");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // Act
        var egg = sav.GenerateEgg(template, out var result);

        // Assert
        result.Should().Be(LegalizationResult.Regenerated);
        egg.Should().NotBeNull();

        var pk9 = (PK9)egg;
        pk9.OriginalTrainerFriendship.Should().BeGreaterOrEqualTo(1, "蛋应该有最小孵化周期");
        pk9.OriginalTrainerFriendship.Should().BeLessOrEqualTo(pk9.PersonalInfo.HatchCycles, "蛋的亲密度不应超过该物种的孵化周期");
    }

    [Fact]
    public void EggShouldHaveEggMoves()
    {
        // Arrange
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        var set = new ShowdownSet(@"Egg (Sprigatito) (M)
Ability: Overgrow
Ball: Poke Ball
- Scratch
- Tail Whip
- Leafage");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // Act
        var egg = sav.GenerateEgg(template, out var result);

        // Assert
        result.Should().Be(LegalizationResult.Regenerated);
        egg.Should().NotBeNull();

        var pk9 = (PK9)egg;
        pk9.Move1.Should().NotBe(0, "蛋应该至少有一个招式");

        var la = new LegalityAnalysis(pk9);
        la.Valid.Should().BeTrue($"带有招式的蛋应该是合法的:\n{la.Report()}");
    }

    [Fact]
    public void EggNicknameShouldBeCorrect()
    {
        // Arrange
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        var set = new ShowdownSet(@"Egg (Sprigatito) (M)
Ability: Overgrow
Ball: Poke Ball");

        var template = AutoLegalityWrapper.GetTemplate(set);

        // Act
        var egg = sav.GenerateEgg(template, out var result);

        // Assert
        result.Should().Be(LegalizationResult.Regenerated);
        egg.Should().NotBeNull();

        var pk9 = (PK9)egg;
        pk9.Nickname.Should().Be("Egg", "未孵化的蛋在英文版中应该叫做 'Egg'");
        pk9.IsNicknamed.Should().BeTrue("蛋应该设置昵称标志");
    }
}
