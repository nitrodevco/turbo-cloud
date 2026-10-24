using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Furniture;
using Turbo.Furniture;
using Turbo.Furniture.Configuration;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Wired matrix W3 action.7.X2 (official client capture 2026-10-24T08-57-51, 2/2 + 1/1 + 1/1):
/// the Furni Name placeholder puts the furnidata public name in the text ("f=Small Block"), and
/// with "show multiple" every pick's in pick order joined by the delimiter ("Small Block + Small
/// Block + Pura lamp"). Ours put the class name.
/// </summary>
public sealed class WiredFurniPublicNameTests
{
    private const int PLACEHOLDER = 1;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Action_7_X2_furni_name_is_the_public_name_and_unknown_names_stay_literal()
    {
        _room.AddFloorItem(9, 4, 4, className: "bb_block_small", publicName: "Small Block");

        var ctx = await PlaceholderAsync(showMultiple: false, "fn", 9);

        (await ctx.FormatTextAsync("f=$(fn) x=$(nope) y=$(FN)", Ct))
            .Should()
            .Be("f=Small Block x=$(nope) y=$(FN)");
    }

    [Theory]
    [InlineData(true, "Small Block + Small Block + Pura lamp")]
    [InlineData(false, "Small Block")]
    public async Task Action_7_X2_show_multiple_joins_the_public_names_in_pick_order(
        bool showMultiple,
        string shown
    )
    {
        _room.AddFloorItem(9, 4, 4, className: "bb_block_small", publicName: "Small Block");
        _room.AddFloorItem(10, 5, 4, className: "bb_block_small", publicName: "Small Block");
        _room.AddFloorItem(11, 6, 4, className: "lamp_basic", publicName: "Pura lamp");

        var ctx = await PlaceholderAsync(showMultiple, "fn\t + ", 9, 10, 11);

        (await ctx.FormatTextAsync("$(fn)", Ct)).Should().Be(shown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task Action_7_X2_furni_without_a_public_name_shows_its_class_name(
        string? publicName
    )
    {
        _room.AddFloorItem(9, 4, 4, className: "custom_statue", publicName: publicName);

        var ctx = await PlaceholderAsync(showMultiple: false, "fn", 9);

        (await ctx.FormatTextAsync("$(fn)", Ct)).Should().Be("custom_statue");
    }

    /// <summary>The room's furni definitions come from the definition provider: it carries the furnidata name.</summary>
    [Fact]
    public async Task Action_7_X2_the_definition_provider_carries_the_furnidata_public_name()
    {
        using var db = new SqliteDb();

        db.Insert(Definition(1, "bb_block_small", "Small Block"));
        db.Insert(Definition(2, "custom_statue", null));

        var provider = new FurnitureDefinitionProvider(
            Options.Create(new FurnitureConfig()),
            db,
            NullLogger<IFurnitureDefinitionProvider>.Instance
        );

        await provider.ReloadAsync(Ct);

        provider.TryGetDefinition(1)!.DisplayName.Should().Be("Small Block");
        provider.TryGetDefinition(2)!.DisplayName.Should().Be("custom_statue");
    }

    private async Task<WiredExecutionContext> PlaceholderAsync(
        bool showMultiple,
        string stringParam,
        params int[] picks
    )
    {
        var placeholder = _room.AddBox<WiredAddonFurniNamePlaceholder>(
            PLACEHOLDER,
            0,
            0,
            "wf_xtra_text_output_furni_name"
        );

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                PLACEHOLDER,
                intParams: [showMultiple ? 1 : 0],
                stringParam: stringParam,
                stuffIds: picks,
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Policy.TextPlaceholders.Add(placeholder);

        return ctx;
    }

    private static FurnitureDefinitionEntity Definition(int id, string name, string? publicName) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = "default_floor",
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            PublicName = publicName,
        };
}
