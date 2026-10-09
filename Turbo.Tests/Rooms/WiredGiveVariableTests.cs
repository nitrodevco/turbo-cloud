using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Give Variable's initial value is a long in two ints (Flash <c>Util.pushIntAsLong</c>: -1 then
/// the value for a negative one). The high word was held to 0, so a box with a negative initial
/// value could not be saved at all (tester report 2026-10-09: "give variable doesn't work").
/// </summary>
public sealed class WiredGiveVariableTests
{
    private const int PLAYER_INDEX = 5;
    private const int MARK = 20;
    private const int BOX = 2;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(7)]
    [InlineData(-5)]
    public async Task The_initial_value_is_given_whatever_its_sign(int initial)
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        var mark = _room.AddBox<WiredVariableUser>(MARK, 6, 6, "wf_var_user");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                MARK,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: "mark"
            )
        )
            .Should()
            .BeTrue();
        await mark.LoadWiredAsync(Ct);
        await _room
            .Harness.Module<RoomWiredSystem>()
            .OnRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    StackIds = [_room.Map.ToIdx(6, 6)],
                },
                Ct
            );
        await _room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(10_000, dormant: false, Ct);
        var give = _room.AddBox<WiredActionGiveVariable>(BOX, 0, 0, "wf_act_give_var");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [(int)WiredVariableTargetType.User, initial < 0 ? -1 : 0, initial, 0],
                definitionSpecifics: [0],
                variableIds: [mark.GetVarSnapshot().VariableId.ToString()]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);

        (await give.ExecuteAsync(ctx, Ct)).Should().BeTrue();
        mark.TryGetValue(
                new WiredVariableKey(
                    mark.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.User,
                    PLAYER_INDEX
                ),
                out var value
            )
            .Should()
            .BeTrue();
        value.Should().Be(new WiredVariableValue(initial));
    }
}
