using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Filter furni by variable" keeping one furni, sorted as its editor lists the options
/// (<c>wiredfurni.params.variables.sort_by.0</c> "Highest value", <c>.1</c> "Lowest value"). The
/// selectors picked furni 30, 31 and 32, whose "score" is 1, 5 and 3.
/// </summary>
public sealed class WiredVariableFilterSortTests
{
    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData(0, 31)]
    [InlineData(1, 30)]
    public async Task The_filter_keeps_the_furni_the_sort_puts_first(int sort, int kept)
    {
        foreach (var id in new[] { 30, 31, 32 })
            _room.AddFloorItem(id, id - 29, 5);

        var score = _room.AddBox<WiredVariableFurni>(10, 4, 4, "wf_var_furni");
        var filter = _room.AddBox<WiredAddonFilterFurniByVariable>(
            11,
            0,
            0,
            "wf_xtra_filter_furni_by_var"
        );

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.RoomActive, 1],
                stringParam: "score"
            )
        )
            .Should()
            .BeTrue();
        await Wired.OnRoomEventAsync(
            new WiredVariableBoxChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                BoxIds = [10],
            },
            Ct
        );
        _now += 1_000;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);

        var variableId = score.GetVarSnapshot().VariableId;

        foreach (var (id, value) in new[] { (30, 1), (31, 5), (32, 3) })
            (
                await score.GiveValueAsync(
                    new WiredVariableKey(variableId, WiredVariableTargetType.Furni, id),
                    value
                )
            )
                .Should()
                .BeTrue();

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                11,
                intParams: [1, sort, 0, (int)WiredVariableTargetType.Furni],
                variableIds: [variableId.ToString()]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };
        ctx.SelectorPool.SelectedFurniIds.UnionWith([30, 31, 32]);

        await filter.MutatePolicyAsync(ctx, Ct);

        ctx.SelectorPool.SelectedFurniIds.Should().Equal(kept);
    }
}
