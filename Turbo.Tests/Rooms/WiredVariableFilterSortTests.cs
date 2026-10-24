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

    // addon.13.R1 (two accounts): the user filter keeps the users the variable ranks first in the
    // SELECTION's order, and drops a user without the variable whatever the amount (official
    // client capture 2026-10-24T16-05-07 / 16-05-46: "uq" Hutch. 9, results! 5, bot none; amount 2
    // highest and lowest both "Hutch.,results!", amount 3 the same).
    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    public async Task Addon13R1_KeptUsersStayInTheSelectionOrder(int amount, int sort)
    {
        var uq = await UserVariableAsync(new() { [5] = 5, [6] = 9 });
        var filter = await UserFilterAsync(11, amount, sort, uq);
        var ctx = PoolOf(6, 5, 7);

        await filter.MutatePolicyAsync(ctx, Ct);

        ctx.SelectorPool.SelectedAvatarIds.Select(x => x.Value).Should().Equal(6, 5);
    }

    // addon.13.R2 (two accounts): two user filters, highest 1 and lowest 1, run one after the
    // other in an order that changes between firings: one user or the other was kept, 5 and 3 of
    // 8 (official client capture 2026-10-24T16-06-12 / 16-06-33; furni filters alike, addon.12.R2).
    [Fact]
    public async Task Addon13R2_TwoFiltersRunInARandomOrder()
    {
        var uq = await UserVariableAsync(new() { [5] = 5, [6] = 9 });
        List<IWiredAddon> addons =
        [
            await UserFilterAsync(11, 1, 0, uq),
            await UserFilterAsync(12, 1, 1, uq),
        ];
        var kept = new HashSet<int>();

        for (var run = 0; run < 64; run++)
        {
            var ctx = PoolOf(6, 5);

            foreach (var addon in RoomWiredSystem.AddonsInRunOrder(addons))
                await addon.MutatePolicyAsync(ctx, Ct);

            ctx.SelectorPool.SelectedAvatarIds.Should().ContainSingle();
            kept.UnionWith(ctx.SelectorPool.SelectedAvatarIds.Select(x => x.Value));
        }

        kept.Should().BeEquivalentTo([5, 6]);
    }

    private async Task<WiredVariableUser> UserVariableAsync(Dictionary<int, int> values)
    {
        foreach (var objectId in new[] { 5, 6, 7 })
            _room.Enter(objectId, objectId - 4, 1);

        var variable = _room.AddBox<WiredVariableUser>(10, 4, 4, "wf_var_user");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: "uq"
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

        var variableId = variable.GetVarSnapshot().VariableId;

        foreach (var (objectId, value) in values)
            (
                await variable.GiveValueAsync(
                    new WiredVariableKey(variableId, WiredVariableTargetType.User, objectId),
                    value
                )
            )
                .Should()
                .BeTrue();

        return variable;
    }

    private async Task<WiredAddonFilterUsersByVariable> UserFilterAsync(
        int id,
        int amount,
        int sort,
        WiredVariableUser variable
    )
    {
        var filter = _room.AddBox<WiredAddonFilterUsersByVariable>(
            id,
            id - 10,
            0,
            "wf_xtra_filter_users_by_var"
        );

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                id,
                intParams: [amount, sort, 0, (int)WiredVariableTargetType.User],
                variableIds: [variable.GetVarSnapshot().VariableId.ToString()]
            )
        )
            .Should()
            .BeTrue();

        return filter;
    }

    private WiredProcessingContext PoolOf(params int[] avatarIds)
    {
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

        foreach (var id in avatarIds)
            ctx.SelectorPool.SelectedAvatarIds.Add(id);

        return ctx;
    }
}
