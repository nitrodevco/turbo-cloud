using System.Reflection;
using FluentAssertions;
using Turbo.Database.Migrations;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>Every logic the configuration furni migrations give a furni is one the room registers.</summary>
public sealed class ConfigurationFurniMigrationTests
{
    [Theory]
    [InlineData(
        typeof(MapConfigurationFurniLogic),
        new[] { "conf_area_hide", "conf_invis_control" }
    )]
    [InlineData(
        typeof(MapConfigurationControllerLogic),
        new[] { "conf_handitem_block", "conf_chooser_disable" }
    )]
    [InlineData(
        typeof(MapRollerSpeedAndWiredDisableLogic),
        new[] { "conf_queue_speed", "conf_wired_disable" }
    )]
    public void The_configuration_furni_get_logics_the_room_registers(
        Type migration,
        string[] names
    )
    {
        var logics = ((string Name, string Logic)[])
            migration
                .GetField("LOGICS", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;
        var registered = typeof(FurnitureAreaHideLogic)
            .Assembly.GetTypes()
            .Select(x => x.GetCustomAttribute<RoomObjectLogicAttribute>()?.Key)
            .OfType<string>()
            .ToHashSet();

        logics.Select(x => x.Name).Should().BeEquivalentTo(names);
        logics.Select(x => x.Logic).Should().OnlyContain(x => registered.Contains(x));
    }
}
