using FluentAssertions;
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
/// "Remote selection" combines the selections of the stacks it picks
/// (<c>wiredfurni.params.remote_selection.type.0</c> / <c>.1</c>: union, intersection) and can use a
/// random number of them (<c>remote_selection.filter.1</c>). It picks two "selected furni" boxes:
/// one selecting 30 and 31, the other 31 and 32.
/// </summary>
public sealed class WiredRemoteSelectionTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Theory]
    [InlineData(0, 0, new[] { 30, 31, 32 })]
    [InlineData(1, 0, new[] { 31 })]
    public async Task The_picked_stacks_selections_are_combined(
        int type,
        int filter,
        int[] expected
    ) => (await SelectAsync(type, filter)).Should().BeEquivalentTo(expected);

    [Fact]
    public async Task A_random_amount_uses_that_many_of_the_picked_stacks()
    {
        var selected = await SelectAsync(0, 1);

        selected
            .Should()
            .Match<int[]>(s =>
                s.SequenceEqual(new[] { 30, 31 }) || s.SequenceEqual(new[] { 31, 32 })
            );
    }

    private async Task<int[]> SelectAsync(int type, int filter)
    {
        foreach (var id in new[] { 30, 31, 32 })
            _room.AddFloorItem(id, id - 29, 5);

        _room.AddBox<WiredSelectorSelectedItems>(1, 0, 0, "wf_slc_furni_picks");
        _room.AddBox<WiredSelectorSelectedItems>(2, 0, 1, "wf_slc_furni_picks");
        var remote = _room.AddBox<WiredSelectorRemoteSelection>(3, 0, 2, "wf_slc_remote");

        await SaveSelectorAsync(1, [30, 31]);
        await SaveSelectorAsync(2, [31, 32]);
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                3,
                intParams: [type, filter],
                stuffIds: [1, 2],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [false, false]
            )
        ).Should().BeTrue();

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

        var set = await remote.SelectAsync(ctx, TestContext.Current.CancellationToken);

        return [.. set.SelectedFurniIds.Order()];
    }

    private async Task SaveSelectorAsync(int id, int[] picks) => (
            await _room.SaveAsync<UpdateSelectorMessage>(
                id,
                stuffIds: picks,
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [false, false]
            )
        ).Should().BeTrue();
}
