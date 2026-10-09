using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "User clicks User" and its two options (tester report 2026-10-09: "Do not open avatar menu"
/// and "Do not rotate" did nothing). While the room has the trigger the client is told so
/// (Flash <c>WiredEnvironment</c>), stops turning its avatar and holds the avatar menu until the
/// room answers its <c>WiredClickUser</c> (<c>AvatarInfoWidget.onUserClickHandledEvent</c>). The
/// room never said so, so the client opened the menu and turned on every click.
/// </summary>
public sealed class WiredClickUserEnvironmentTests
{
    private const int CLICKER = 5;
    private const int CLICKED = 6;
    private const int TRIGGER = 1;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task The_room_says_it_has_the_trigger_when_it_is_placed_and_to_whoever_walks_in()
    {
        await PlaceTriggerAsync(blockMenu: false, doNotRotate: false);

        SentToRoom<WiredEnvironmentMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.HasClickUserWired.Should()
            .BeTrue();

        await Wired.OnRoomEventAsync(
            new PlayerEnterEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForPlayer((PlayerId)(100 + CLICKER), (RoomId)1),
                PlayerId = (PlayerId)(100 + CLICKER),
            },
            Ct
        );

        SentTo<WiredEnvironmentMessageComposer>(100 + CLICKER)
            .Should()
            .ContainSingle()
            .Which.HasClickUserWired.Should()
            .BeTrue();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task A_click_is_answered_and_turns_the_clicker_as_the_options_say(
        bool blockMenu,
        bool doNotRotate
    )
    {
        var clicker = _room.Enter(CLICKER, 1, 1);
        _room.Enter(CLICKED, 3, 3);
        clicker.SetBodyRotation(Rotation.North);
        await PlaceTriggerAsync(blockMenu, doNotRotate);

        await _room.Harness.Room.WiredClickAvatarAsync(
            ActionContext.CreateForPlayer((PlayerId)(100 + CLICKER), (RoomId)1),
            CLICKED,
            Ct
        );

        var answer = SentTo<WiredClickUserResponseMessageComposer>(100 + CLICKER)
            .Should()
            .ContainSingle()
            .Subject;
        answer.ObjectId.Should().Be((RoomObjectId)CLICKED);
        answer.OpenMenu.Should().Be(!blockMenu);
        clicker.Rotation.Should().Be(doNotRotate ? Rotation.North : Rotation.SouthEast);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Only_the_reported_click_fires_the_trigger(bool plainClick, bool fires)
    {
        _room.Enter(CLICKER, 1, 1);
        _room.Enter(CLICKED, 3, 3);
        var mark = _room.AddBox<WiredVariableUser>(12, 6, 6, "wf_var_user");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                12,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: "mark"
            )
        )
            .Should()
            .BeTrue();
        await mark.LoadWiredAsync(Ct);
        await RebuildAsync(6, 6);
        _room.AddBox<WiredActionGiveVariable>(2, 0, 0, "wf_act_give_var");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [(int)WiredVariableTargetType.User, 0, 7, 0],
                definitionSpecifics: [0],
                variableIds: [mark.GetVarSnapshot().VariableId.ToString()]
            )
        )
            .Should()
            .BeTrue();
        await PlaceTriggerAsync(blockMenu: false, doNotRotate: false);
        var ctx = ActionContext.CreateForPlayer((PlayerId)(100 + CLICKER), (RoomId)1);

        if (plainClick)
            await _room.Harness.Room.ClickAvatarAsync(ctx, CLICKED, Ct);
        else
            await _room.Harness.Room.WiredClickAvatarAsync(ctx, CLICKED, Ct);

        for (var i = 0; i < 3; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }

        mark.TryGetValue(
                new WiredVariableKey(
                    mark.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.User,
                    CLICKER
                ),
                out _
            )
            .Should()
            .Be(fires);
    }

    private async Task PlaceTriggerAsync(bool blockMenu, bool doNotRotate)
    {
        _room.AddBox<WiredTriggerClickUser>(TRIGGER, 0, 0, "wf_trg_click_user");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                intParams: [blockMenu ? 1 : 0, doNotRotate ? 1 : 0]
            )
        )
            .Should()
            .BeTrue();

        await RebuildAsync(0, 0);
    }

    private async Task RebuildAsync(int x, int y)
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
        _now += 1_000;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
    }

    private IEnumerable<T> SentToRoom<T>() =>
        _room
            .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<T>();

    private IEnumerable<T> SentTo<T>(long playerId) =>
        _room
            .Harness.Fakes.Log.Calls.Where(x =>
                x.Method == "SendComposerAsync" && x.Key is long key && key == playerId
            )
            .SelectMany(x =>
                x.Args.OfType<IComposer>()
                    .Concat(x.Args.OfType<IEnumerable<IComposer>>().SelectMany(c => c))
            )
            .OfType<T>();
}
