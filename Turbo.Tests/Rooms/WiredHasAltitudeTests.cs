using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Furni Has Altitude (<c>wf_cnd_has_altitude</c>, condition 36) opens with the furni quantifier
/// (quantifierType 1, official client capture 2026-10-20T17-37-19) and honours it: block A at
/// 0.01 and B at 0, "= 0.01": "all" fails, "any" passes; with nothing picked "all" passes and
/// "any" fails (W1 lab condition.36.R1, 2026-10-24T07-37-02, 6/6). It compares each furni's own
/// z exactly, 0.01 counting as 1 (condition.36.R5, 9/9). Ours showed no radio and always
/// required all.
/// </summary>
public sealed class WiredHasAltitudeTests
{
    private const int BOX = 1;
    private const int BLOCK_A = 30;
    private const int BLOCK_B = 31;
    private const int ALL = 0;
    private const int ANY = 1;
    private const int EQUALS = 1;

    private readonly WiredRoom _room = new(8, 8);

    // condition.36.R1
    [Theory]
    [InlineData(ALL, true, false)]
    [InlineData(ANY, true, true)]
    [InlineData(ALL, false, true)]
    [InlineData(ANY, false, false)]
    public async Task Has_altitude_counts_its_furni_with_the_quantifier(
        int quantifier,
        bool picked,
        bool passes
    )
    {
        _room.AddFloorItem(BLOCK_A, 3, 3).SetPositionZ(Altitude.FromInt(1));
        _room.AddFloorItem(BLOCK_B, 4, 4);

        var condition = await SaveAsync(1, EQUALS, quantifier, picked ? [BLOCK_A, BLOCK_B] : []);

        condition.Evaluate(Context()).Should().Be(passes);
    }

    // condition.36.R5: A on block B at z 1.00, then on the floor at 0.01.
    [Theory]
    [InlineData(100, 100, EQUALS, true)]
    [InlineData(100, 99, EQUALS, false)]
    [InlineData(100, 99, 2, true)]
    [InlineData(100, 100, 0, false)]
    [InlineData(100, 101, 0, true)]
    [InlineData(100, 100, 2, false)]
    [InlineData(1, 1, EQUALS, true)]
    [InlineData(1, 0, EQUALS, false)]
    [InlineData(1, 0, 2, true)]
    public async Task Has_altitude_compares_the_furnis_own_z_in_hundredths(
        int z,
        int altitude,
        int comparison,
        bool passes
    )
    {
        _room.AddFloorItem(BLOCK_A, 3, 3).SetPositionZ(Altitude.FromInt(z));

        var condition = await SaveAsync(altitude, comparison, ALL, [BLOCK_A]);

        condition.Evaluate(Context()).Should().Be(passes);
    }

    private async Task<WiredConditionItemHeightMatch> SaveAsync(
        int altitude,
        int comparison,
        int quantifier,
        int[] picks
    )
    {
        var condition = _room.AddBox<WiredConditionItemHeightMatch>(
            BOX,
            0,
            0,
            "wf_cnd_has_altitude"
        );

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [altitude, comparison],
                stuffIds: picks,
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [quantifier]
            )
        ).Should().BeTrue();

        return condition;
    }

    private WiredProcessingContext Context() =>
        new(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };
}
