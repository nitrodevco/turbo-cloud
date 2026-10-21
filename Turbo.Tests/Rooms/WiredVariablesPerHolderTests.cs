using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// TOO_MANY_VARIABLES (monitor error 6, <c>room.error.wired.too_many_variables</c>: "The limit for
/// variables per furni/user was reached!"): a user or furni holds a bounded number of created
/// variables. Habbo does not disclose the threshold, so it is configured; here it is 2.
/// </summary>
public sealed class WiredVariablesPerHolderTests
{
    private const int PLAYER_INDEX = 5;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredVariablesPerHolderTests() =>
        _room.Harness.Room.UseWiredConfig(new WiredConfig { MaxVariablesPerHolder = 2 });

    [Fact]
    public async Task A_give_past_the_limit_is_refused_and_logged()
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        var first = await PlaceAsync(20, 4, 4, "first");
        var second = await PlaceAsync(21, 5, 5, "second");
        var third = await PlaceAsync(22, 6, 6, "third");

        (await first.GiveValueAsync(Key(first), new WiredVariableValue(1))).Should().BeTrue();
        (await second.GiveValueAsync(Key(second), new WiredVariableValue(1))).Should().BeTrue();
        (await third.GiveValueAsync(Key(third), new WiredVariableValue(1))).Should().BeFalse();
        third.TryGetValue(Key(third), out _).Should().BeFalse();

        // Overriding a variable the user already holds adds nothing to count.
        (await first.GiveValueAsync(Key(first), new WiredVariableValue(2), replace: true))
            .Should()
            .BeTrue();

        var errors = Wired.GetErrorLogs(0);

        errors[6].ErrorName.Should().Be("TOO_MANY_VARIABLES");
        errors[6].ThrowCount.Should().Be(1);
    }

    private static WiredVariableKey Key(WiredVariableUser variable) =>
        new(variable.GetVarSnapshot().VariableId, WiredVariableTargetType.User, PLAYER_INDEX);

    private async Task<WiredVariableUser> PlaceAsync(int id, int x, int y, string name)
    {
        var variable = _room.AddBox<WiredVariableUser>(id, x, y, "wf_var_user");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                id,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: name
            )
        )
            .Should()
            .BeTrue();
        await variable.LoadWiredAsync(Ct);
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );
        _now += 1_000;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);

        return variable;
    }
}
