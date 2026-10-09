using System.Reflection;
using FluentAssertions;
using Turbo.Database.Migrations;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>Every logic <c>MapConfigurationFurniLogic</c> gives a furni is one the room registers.</summary>
public sealed class ConfigurationFurniMigrationTests
{
    [Fact]
    public void The_configuration_furni_get_logics_the_room_registers()
    {
        var logics = ((string Name, string Logic)[])
            typeof(MapConfigurationFurniLogic)
                .GetField("LOGICS", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;
        var registered = typeof(FurnitureAreaHideLogic)
            .Assembly.GetTypes()
            .Select(x => x.GetCustomAttribute<RoomObjectLogicAttribute>()?.Key)
            .OfType<string>()
            .ToHashSet();

        logics
            .Select(x => x.Name)
            .Should()
            .BeEquivalentTo(["conf_area_hide", "conf_invis_control"]);
        logics.Select(x => x.Logic).Should().OnlyContain(x => registered.Contains(x));
    }
}
