using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "User clicks User": the one who clicks is the triggering user and the one clicked is "The
/// clicked user", a source the boxes on the stack offer. The Wired Faculty tutorial "See who is
/// clicking on who" (19/03/2025) has a bot whisper "$(user) clicks on $(target)", with the
/// "target" placeholder set to "The clicked user". Both users were the triggering users and no
/// box offered the clicked user.
/// </summary>
public sealed class WiredClickedUserSourceTests
{
    private const int CLICKER = 5;
    private const int CLICKED = 6;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private WiredVariableUser _mark = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData(WiredPlayerSourceType.TriggeredUser, CLICKER, CLICKED)]
    [InlineData(WiredPlayerSourceType.ClickedUser, CLICKED, CLICKER)]
    public async Task The_source_picks_the_clicker_or_the_clicked(
        WiredPlayerSourceType source,
        int marked,
        int unmarked
    )
    {
        _room.Enter(CLICKER, 1, 1);
        _room.Enter(CLICKED, 2, 2);
        _mark = _room.AddBox<WiredVariableUser>(12, 6, 6, "wf_var_user");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                12,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: "mark"
            )
        )
            .Should()
            .BeTrue();
        await _mark.LoadWiredAsync(Ct);
        await StartAsync(6, 6);

        _room.AddBox<WiredTriggerClickUser>(1, 0, 0, "wf_trg_click_user");
        var give = _room.AddBox<WiredActionGiveVariable>(2, 0, 0, "wf_act_give_var");

        (await _room.SaveAsync<UpdateTriggerMessage>(1, intParams: [0, 0])).Should().BeTrue();
        give.GetAllowedPlayerSources()
            .Should()
            .ContainSingle()
            .Which.Should()
            .NotContain(WiredPlayerSourceType.ClickedUser);
        give.GetOfferedPlayerSources()
            .Single()
            .Should()
            .StartWith([WiredPlayerSourceType.TriggeredUser, WiredPlayerSourceType.ClickedUser]);
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [(int)WiredVariableTargetType.User, 0, 7, 0],
                definitionSpecifics: [0],
                playerSources:
                [
                    [source],
                ],
                variableIds: [_mark.GetVarSnapshot().VariableId.ToString()]
            )
        ).Should().BeTrue();
        give.GetPlayerSources().Single().Should().Equal(source);
        await StartAsync(0, 0);

        await Wired.OnRoomEventAsync(
            new PlayerClickedAvatarEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForPlayer((PlayerId)(100 + CLICKER), (RoomId)1),
                PlayerId = (PlayerId)(100 + CLICKER),
                TargetObjectId = CLICKED,
            },
            Ct
        );
        await TickAsync(3);

        Holds(marked).Should().BeTrue();
        Holds(unmarked).Should().BeFalse();
    }

    private bool Holds(int objectId) =>
        _mark.TryGetValue(
            new WiredVariableKey(
                _mark.GetVarSnapshot().VariableId,
                WiredVariableTargetType.User,
                objectId
            ),
            out _
        );

    private async Task StartAsync(int x, int y)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );

        await TickAsync(1);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
