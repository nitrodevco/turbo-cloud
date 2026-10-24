using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The "in neighbourhood" selectors' params: [around users (1) or furni (0), root x, root y, 15
/// mask ints]. The editor sends 14 mask ints and Habbo stores 15, padding with 0 and dropping
/// any further one; a root past 64 is clamped; "In neighbourhood of" users reads the user slot
/// only, furni the furni slot only (official client capture 2026-10-22T22-51-19-click /
/// 22-52-15-click / 22-53-42-UpdateSelector).
/// </summary>
public sealed class WiredNeighborhoodParamsTests
{
    private const int BOX = 1;
    private const int CENTRE = 30;
    private const int NEXT_TO_IT = 31;
    private const int SQUARE = 33554431;

    private readonly WiredRoom _room = new(12, 12);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredSelectorItemsInNeighborhood _selector;

    public WiredNeighborhoodParamsTests()
    {
        _room.AddFloorItem(CENTRE, 5, 5);
        _room.AddFloorItem(NEXT_TO_IT, 6, 5);
        _selector = _room.AddBox<WiredSelectorItemsInNeighborhood>(
            BOX,
            0,
            0,
            "wf_slc_furni_neighborhood"
        );
    }

    [Fact]
    public async Task The_editors_fourteen_mask_ints_are_stored_as_fifteen()
    {
        (await SaveAsync([1, 0, 0, SQUARE, .. new int[13]])).IsSaved.Should().BeTrue();

        _selector.GetSnapshot().IntParams.Should().Equal([1, 0, 0, SQUARE, .. new int[14]]);
    }

    [Fact]
    public async Task Ints_past_the_fifteenth_mask_int_are_dropped()
    {
        (await SaveAsync([1, 0, 0, SQUARE, .. new int[14], 7])).IsSaved.Should().BeTrue();

        _selector.GetSnapshot().IntParams.Should().HaveCount(18);
    }

    [Fact]
    public async Task A_root_past_64_is_clamped()
    {
        (await SaveAsync([1, 65, -65, SQUARE, .. new int[13]])).IsSaved.Should().BeTrue();

        _selector.GetSnapshot().IntParams.Take(3).Should().Equal(1, 64, -64);
    }

    [Theory]
    [InlineData(0, new[] { CENTRE, NEXT_TO_IT })]
    [InlineData(1, new int[0])]
    public async Task The_neighbourhood_is_around_the_furni_or_the_users_only(
        int users,
        int[] selected
    )
    {
        (await SaveAsync([users, 0, 0, SQUARE, .. new int[13]], stuffIds: [CENTRE]))
            .IsSaved.Should()
            .BeTrue();

        var set = await _selector.SelectAsync(Context(), Ct);

        set.SelectedFurniIds.Order().Should().Equal(selected);
    }

    // selector.6.R5: a mask of the tile one step +X of the picked furni selects the furni there,
    // the tile one step -X (spiral rank 5) selects nothing when it is empty (confidential capture,
    // measured: relative neighbourhood masks).
    [Theory]
    [InlineData(1 << 1, new[] { NEXT_TO_IT })]
    [InlineData(1 << 5, new int[0])]
    public async Task Selector6R5_OneMaskedTileBesideTheFurni_SelectsWhatStandsThere(
        int mask,
        int[] selected
    )
    {
        (await SaveAsync([0, 0, 0, mask, .. new int[13]], stuffIds: [CENTRE]))
            .IsSaved.Should()
            .BeTrue();

        var set = await _selector.SelectAsync(Context(), Ct);

        set.SelectedFurniIds.Order().Should().Equal(selected);
    }

    private Task<WiredSaveResult> SaveAsync(int[] intParams, int[]? stuffIds = null) =>
        _room.SaveWithResultAsync<UpdateSelectorMessage>(
            BOX,
            intParams: intParams,
            stuffIds: stuffIds,
            furniSources:
            [
                [WiredFurniSourceType.SelectedItems],
            ],
            definitionSpecifics: [false, false]
        );

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
