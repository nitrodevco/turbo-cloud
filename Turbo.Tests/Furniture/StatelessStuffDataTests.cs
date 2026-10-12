using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Extensions;
using Turbo.Furniture;
using Turbo.Furniture.Providers;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Xunit;

namespace Turbo.Tests.Furniture;

/// <summary>
/// A room's furni as Habbo sends them (hhus, 2026-10-12, a starter room): a stateless item
/// (floor tiles, modules, chairs) that never stored any data has empty stuff data, not "0", and
/// a shelf's stack height is 0.6875, not 0.69.
/// </summary>
public sealed class StatelessStuffDataTests
{
    private readonly IStuffDataFactory _factory = new StuffDataFactory(
        NullLogger<IStuffDataFactory>.Instance
    );

    [Fact]
    public void A_stateless_item_with_nothing_stored_has_empty_data()
    {
        _factory
            .CreateStuffDataFromExtraData(
                StuffDataType.LegacyKey,
                new ExtraData(null),
                string.Empty
            )
            .GetLegacyString()
            .Should()
            .BeEmpty();
        _factory
            .CreateStuffDataFromExtraData(StuffDataType.LegacyKey, new ExtraData(null))
            .GetLegacyString()
            .Should()
            .Be("0");
    }

    [Fact]
    public void A_stored_state_is_kept_whatever_the_items_states()
    {
        var data = _factory.CreateStuffDataFromExtraData(
            StuffDataType.LegacyKey,
            new ExtraData("""{"stuff":{"Data":"4"}}"""),
            string.Empty
        );

        data.GetLegacyString().Should().Be("4");
        data.GetState().Should().Be(4);
    }

    [Fact]
    public void Empty_data_reads_as_state_zero()
    {
        _factory
            .CreateStuffDataFromExtraData(
                StuffDataType.LegacyKey,
                new ExtraData(null),
                string.Empty
            )
            .GetState()
            .Should()
            .Be(0);
    }

    [Fact]
    public void A_definitions_stack_height_keeps_four_decimals()
    {
        var entity = (FurnitureDefinitionEntity)
            RuntimeHelpers.GetUninitializedObject(typeof(FurnitureDefinitionEntity));
        entity.Name = "usva_nt_shelf";
        entity.Logic = "default_floor";
        entity.StackHeight = 0.6875;

        entity.ToSnapshot(0).StackHeight.Value.Should().Be(0.6875);
    }
}
