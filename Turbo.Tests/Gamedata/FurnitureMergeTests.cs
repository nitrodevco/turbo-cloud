using System.Text.Json.Nodes;
using FluentAssertions;
using Turbo.Database.Entities.Furniture;
using Turbo.Gamedata.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// An import compares each field three ways - Habbo's new value, Habbo's value when last taken
/// in, the hotel's - so Habbo's updates come in and the hotel's own changes stay.
/// </summary>
public sealed class FurnitureMergeTests
{
    [Fact]
    public void a_field_the_hotel_left_as_habbo_had_it_takes_habbos_new_value()
    {
        var definition = Chair(width: 1);
        var before = Habbo(xdim: 1);
        var now = Habbo(xdim: 2);

        var changes = FurnitureMerge.Compare(definition, now, before);

        changes.Should().ContainSingle(x => x.Field == "xdim" && !x.Kept);
    }

    [Fact]
    public void a_field_the_hotel_changed_keeps_the_hotels_value_and_is_reported()
    {
        var definition = Chair(width: 3);
        var before = Habbo(xdim: 1);
        var now = Habbo(xdim: 2);

        var changes = FurnitureMerge.Compare(definition, now, before);

        changes.Should().ContainSingle(x => x.Field == "xdim" && x.Kept && x.Current == "3");
    }

    [Fact]
    public void a_field_only_the_hotel_changed_says_nothing()
    {
        var definition = Chair(width: 3);
        var before = Habbo(xdim: 1);
        var now = Habbo(xdim: 1);

        FurnitureMerge.Compare(definition, now, before).Should().BeEmpty();
    }

    [Fact]
    public void without_an_earlier_habbo_value_client_columns_take_habbos_and_the_rest_keep_the_hotels()
    {
        var definition = Chair(width: 3);
        var now = Habbo(xdim: 2, name: "Dining Chair");

        var changes = FurnitureMerge.Compare(definition, now, null);

        changes.Should().ContainSingle(x => x.Field == "name" && !x.Kept);
        changes.Should().ContainSingle(x => x.Field == "xdim" && x.Kept);
    }

    [Fact]
    public void without_an_earlier_habbo_value_the_files_states_replace_the_hotels()
    {
        // The hotel's definitions came with 3; the file (states 0 and 1, transitions 100 and 101) says 2.
        var definition = Chair(width: 1);
        var now = Habbo(xdim: 1);

        definition.TotalStates = 3;
        now["states"] = 2;

        var changes = FurnitureMerge.Compare(definition, now, null);

        changes.Should().ContainSingle(x => x.Field == "states" && !x.Kept && x.Incoming == "2");
    }

    [Fact]
    public void values_are_compared_as_the_columns_hold_them()
    {
        // "1" is 1, 1 is true, and the four-decimal stack height holds 0.00001 as 0.
        var definition = Chair(width: 1, height: 0);
        var now = Habbo(xdim: 1);

        now["xdim"] = "1";
        now["canputstuffon"] = 1;
        now["height"] = 0.00001;

        var changes = FurnitureMerge.Compare(definition, now, now);

        changes.Should().NotContain(x => x.Field == "xdim");
        changes.Should().NotContain(x => x.Field == "canputstuffon");
        changes.Should().NotContain(x => x.Field == "height");
    }

    [Fact]
    public void a_wall_item_has_no_footprint_or_seating_to_compare()
    {
        var definition = Chair(width: 9);

        definition.ProductType = ProductType.Wall;

        var changes = FurnitureMerge.Compare(definition, Habbo(xdim: 1), null);

        changes.Should().NotContain(x => x.Field == "xdim" || x.Field == "canstandon");
    }

    private static JsonObject Habbo(int xdim, string name = "Chair") =>
        new()
        {
            ["id"] = 30,
            ["classname"] = "chair_norja",
            ["revision"] = 0,
            ["category"] = null,
            ["defaultdir"] = 0,
            ["xdim"] = xdim,
            ["ydim"] = 1,
            ["name"] = name,
            ["description"] = null,
            ["adurl"] = null,
            ["excludeddynamic"] = false,
            ["customparams"] = null,
            ["specialtype"] = 1,
            ["canstandon"] = false,
            ["cansiton"] = true,
            ["canlayon"] = false,
            ["canputstuffon"] = true,
            ["height"] = 0,
            ["furniline"] = null,
            ["environment"] = null,
            ["rare"] = false,
            ["tradeable"] = true,
            ["recyclable"] = true,
        };

    private static FurnitureDefinitionEntity Chair(int width, double height = 0) =>
        new()
        {
            SpriteId = 30,
            Name = "chair_norja",
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = "default_floor",
            Width = width,
            Length = 1,
            StackHeight = height,
            CanStack = true,
            CanWalk = false,
            CanSit = true,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            PublicName = "Chair",
        };
}
