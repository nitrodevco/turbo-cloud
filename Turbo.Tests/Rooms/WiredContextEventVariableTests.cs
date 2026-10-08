using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.Context;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The context variables sirjonasxx lists for the wired execution itself (variables-info #9 and
/// #13): <c>@chat_type</c> and <c>@chat_style</c> "if the wired stack was triggered by User Says
/// Keyword" ("check if the user is chatting with a Zombie Hand chat bubble"), and for a signal
/// <c>@antenna_id</c>, <c>@signal_furni_count</c> and <c>@signal_user_count</c>. None existed.
/// </summary>
public sealed class WiredContextEventVariableTests
{
    private const int PLAYER_INDEX = 5;
    private const int ANTENNA = 21;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task A_said_keyword_gives_its_chat_type_and_bubble_style()
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        var chat = new PlayerChatEvent
        {
            RoomId = 1,
            CausedBy = ActionContext.CreateForPlayer((PlayerId)(100 + PLAYER_INDEX), (RoomId)1),
            PlayerId = (PlayerId)(100 + PLAYER_INDEX),
            ObjectId = PLAYER_INDEX,
            ChatType = RoomChatType.Shout,
            StyleId = 7,
            TrackingId = 0,
            Gesture = default,
            Text = "go",
        };

        (await RunAsync(new ContextChatTypeVariable(_room.Harness.Room), KeywordAsync, chat))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task A_said_keyword_gives_its_bubble_style()
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        var chat = new PlayerChatEvent
        {
            RoomId = 1,
            CausedBy = ActionContext.CreateForPlayer((PlayerId)(100 + PLAYER_INDEX), (RoomId)1),
            PlayerId = (PlayerId)(100 + PLAYER_INDEX),
            ObjectId = PLAYER_INDEX,
            ChatType = RoomChatType.Chat,
            StyleId = 7,
            TrackingId = 0,
            Gesture = default,
            Text = "go",
        };

        (await RunAsync(new ContextChatStyleVariable(_room.Harness.Room), KeywordAsync, chat))
            .Should()
            .Be(7);
    }

    [Theory]
    [InlineData("antenna", ANTENNA)]
    [InlineData("furni", 2)]
    [InlineData("users", 1)]
    public async Task A_received_signal_gives_its_antenna_and_what_it_carried(
        string which,
        int expected
    )
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        _room.AddFloorItem(ANTENNA, 7, 1);
        _room.AddFloorItem(30, 6, 6);
        _room.AddFloorItem(31, 6, 7);

        WiredInternalVariable variable = which switch
        {
            "antenna" => new ContextAntennaIdVariable(_room.Harness.Room),
            "furni" => new ContextSignalFurniCountVariable(_room.Harness.Room),
            _ => new ContextSignalUserCountVariable(_room.Harness.Room),
        };
        var signal = new WiredSignalEvent
        {
            RoomId = 1,
            CausedBy = ActionContext.CreateForSystem(1),
            AntennaIds = [ANTENNA],
            FurniIds = [30, 31],
            AvatarIds = [PLAYER_INDEX],
            Depth = 1,
        };

        (await RunAsync(variable, ReceiveAsync, signal)).Should().Be(expected);
    }

    private async Task KeywordAsync()
    {
        _room.AddBox<WiredTriggerHabboSaysKeyword>(1, 0, 0, "wf_trg_says_something");
        (await _room.SaveAsync<UpdateTriggerMessage>(1, intParams: [0, 0, 0], stringParam: "go"))
            .Should()
            .BeTrue();
    }

    private async Task ReceiveAsync()
    {
        _room.AddBox<WiredTriggerReceiveSignal>(1, 0, 0, "wf_trg_recv_signal");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [ANTENNA],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
    }

    /// <summary>
    /// The trigger <paramref name="addTrigger"/> places on (0, 0) fires on <paramref name="evt"/>,
    /// and Change Variable copies the context variable into the global "seen".
    /// </summary>
    private async Task<long> RunAsync(
        WiredInternalVariable variable,
        Func<Task> addTrigger,
        RoomEvent evt
    )
    {
        var id = variable.GetVarSnapshot().VariableId;

        ((IDictionary)RoomHarness.GetMember(_room.Harness.Room.WiredSystem, "_variableById")!)[id] =
            variable;

        var seen = _room.AddBox<WiredVariableRoom>(10, 5, 5, "wf_var_room");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "seen"
            )
        )
            .Should()
            .BeTrue();
        await seen.LoadWiredAsync(Ct);
        await StartAsync(5, 5);

        await addTrigger();
        _room.AddBox<WiredActionChangeVariable>(3, 0, 0, "wf_act_change_var_val");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)WiredVariableOperationType.Set,
                    1,
                    0,
                    0,
                    (int)WiredVariableTargetType.Context,
                ],
                definitionSpecifics: [0],
                variableIds: [seen.GetVarSnapshot().VariableId.ToString(), id.ToString()]
            )
        )
            .Should()
            .BeTrue();
        await StartAsync(0, 0);

        await Wired.OnRoomEventAsync(evt, Ct);
        await TickAsync(4);

        seen.TryGetValue(
                new WiredVariableKey(
                    seen.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.Global,
                    0
                ),
                out var value
            )
            .Should()
            .BeTrue();

        return value.Value;
    }

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
