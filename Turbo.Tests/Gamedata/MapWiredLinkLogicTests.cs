using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Turbo.Database.Migrations;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// Hotels that imported Habbo's WIRED Links furni (2026-10) before the import gave a new
/// definition the logic registered under its classname have them on <c>default_floor</c>.
/// </summary>
public sealed class MapWiredLinkLogicTests
{
    private static readonly string[] NAMES =
    [
        "wf_link",
        "wf_act_send_message",
        "wf_act_send_dataframe",
        "wf_trg_receive_message",
        "wf_trg_receive_dataframe",
        "wf_xtra_link_speaker",
    ];

    [Fact]
    public void Only_a_definition_still_on_the_default_logic_gets_its_wired_links_logic()
    {
        Sql("Up")
            .Should()
            .Equal(
                NAMES.Select(name =>
                    $"UPDATE `furniture_definitions` SET `logic` = '{name}' WHERE (`name` = '{name}' OR (`name` LIKE '%*%' AND SUBSTRING_INDEX(`name`, '*', 1) = '{name}')) AND `logic` IN ('default_floor', '{name}');"
                )
            );
        Sql("Down")
            .Should()
            .Equal(
                NAMES.Select(name =>
                    $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE (`name` = '{name}' OR (`name` LIKE '%*%' AND SUBSTRING_INDEX(`name`, '*', 1) = '{name}')) AND `logic` = '{name}';"
                )
            );
    }

    [Fact]
    public void Each_logic_it_gives_is_one_the_server_registers()
    {
        var registered = typeof(FurnitureWiredLinkLogic)
            .Assembly.GetTypes()
            .Select(x => x.GetCustomAttribute<RoomObjectLogicAttribute>(false)?.Key)
            .OfType<string>()
            .ToHashSet();

        registered.Should().Contain(NAMES);
    }

    private static List<string> Sql(string method)
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        typeof(MapWiredLinkLogic)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(new MapWiredLinkLogic(), [builder]);

        return [.. builder.Operations.OfType<SqlOperation>().Select(x => x.Sql)];
    }
}
