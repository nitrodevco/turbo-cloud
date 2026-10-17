using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A clothing booth's data is its two looks, "boy,girl" (FurnitureClothingChangeLogic.as), and the
/// client puts its default look on a gender only while that look is empty
/// (FurnitureClothingChangeWidgetHandler.as). A booth furni with states (boutique_changing1 has
/// two) sent "0" before it was ever dressed, so the editor opened on the look "0": no figure at
/// all, and the look saved from it had no legs.
/// </summary>
public sealed class ClothingBoothDataTests
{
    private const int BOOTH = 30;
    private const string BOY = "hd-180-1.ch-210-66.lg-270-62";

    private readonly WiredRoom _room = new(8, 8);
    private readonly IRoomFloorItem _booth;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ClothingBoothDataTests()
    {
        // The harness gives every definition two states, as the booth furni has.
        _booth = _room.AddFloorItem(
            BOOTH,
            3,
            3,
            "clothing_change",
            createLogic: (factory, ctx) => new FurnitureClothingChangeLogic(factory, ctx)
        );
    }

    private string SentData() => ((LegacyStuffSnapshot)_booth.Logic.StuffData.GetSnapshot()).Data;

    [Fact]
    public void An_undressed_booth_sends_empty_looks_not_state_zero()
    {
        SentData().Should().BeEmpty();
    }

    [Fact]
    public async Task Dressing_one_gender_leaves_the_other_empty()
    {
        await _booth.Logic.OnInteractAsync(
            ActionContext.CreateForSystem(1),
            new SetClothingChangeInteraction { Gender = AvatarGenderType.Male, Figure = BOY },
            Ct
        );

        SentData().Should().Be($"{BOY},");
    }
}
