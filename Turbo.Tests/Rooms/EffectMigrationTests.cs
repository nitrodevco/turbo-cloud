using System.Reflection;
using FluentAssertions;
using Turbo.Database.Migrations;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>The effect furni the migration maps get one of the four effect logics the room registers.</summary>
public sealed class EffectMigrationTests
{
    [Fact]
    public void Every_effect_furni_gets_a_registered_effect_logic()
    {
        var rows = ((string Name, string Logic)[])
            typeof(MapEffectFurni)
                .GetField("EFFECT_FURNI", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;
        var registered = typeof(FurnitureEffectAreaLogic)
            .Assembly.GetTypes()
            .Select(x => x.GetCustomAttribute<RoomObjectLogicAttribute>()?.Key)
            .OfType<string>()
            .ToHashSet();

        rows.Should().HaveCount(81);
        rows.Select(x => x.Name).Should().OnlyHaveUniqueItems();
        rows.Select(x => x.Logic)
            .Distinct()
            .Should()
            .BeEquivalentTo(["effect_box", "effect_provider", "effect_area", "effect_tile"]);
        rows.Select(x => x.Logic).Should().OnlyContain(x => registered.Contains(x));
        rows.Should().Contain(("room_noob_fxremove", "effect_tile"));
        rows.Should().Contain(("cpunk15_gunvender", "effect_provider"));
    }
}
