using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Events;
using Turbo.Events.Registry;
using Turbo.Primitives.Action;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A sleeping player wakes on anything they do in the room, and the idle tick does not put them
/// straight back to sleep. Driven through the room grain methods the packet handlers call.
/// </summary>
public sealed class IdleWakeTests
{
    private static readonly ActionContext Player = ActionContext.CreateForPlayer(9, 1);

    private readonly LiveRoomHarness _harness = new(10, 10);
    private readonly RoomPlayerAvatar _avatar = new() { ObjectId = 5, PlayerId = 9 };

    public IdleWakeTests()
    {
        _harness.Room.UseEventSystem(
            new EventSystem(
                new EventRegistry(_harness.Services, NullLogger<EventRegistry>.Instance)
            )
        );
        _harness.Room.UseRoomConfig(
            new RoomConfig { ChatFloodWindowMs = 0, ChatlogEnabled = false }
        );

        _harness.State.RoomSnapshot = _harness.State.RoomSnapshot with
        {
            ChatProtection = ChatFloodSensitivityType.Normal,
            IdleSleepEnabled = true,
            IdleSleepTimeoutSeconds = 60,
        };

        _avatar.UseName("sleeper");
        _avatar.SetPosition(2, 2);
        _harness.State.AvatarsByObjectId[5] = _avatar;
        _harness.State.AvatarsByPlayerId[9] = 5;
        _harness.Module<Turbo.Rooms.Grains.Modules.RoomMapModule>().AddAvatar(_avatar, false);
    }

    [Fact]
    public async Task A_dance_is_answered_before_the_wake_as_habbo_does()
    {
        // Habbo (hhus, 2026-10-12), a sleeping avatar told to dance: Dance, then Sleep false.
        await _harness.Room.SetAvatarExpressionAsync(
            Player,
            AvatarExpressionType.Idle,
            CancellationToken.None
        );
        _harness.Fakes.Log.Calls.Clear();

        await _harness.Room.SetAvatarDanceAsync(
            Player,
            AvatarDanceType.Dance,
            CancellationToken.None
        );

        var sent = _harness
            .Fakes.SentToRoom<IComposer>()
            .Select(x => x.GetType().Name)
            .Where(x => x is "DanceMessageComposer" or "SleepMessageComposer")
            .ToList();
        sent.Should().Equal("DanceMessageComposer", "SleepMessageComposer");
    }

    public static IEnumerable<object[]> Actions() =>
        [
            ["walk"],
            ["use furniture"],
            ["click furniture"],
            ["chat"],
        ];

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task A_sleeping_player_wakes_and_stays_awake_when_they_act(string action)
    {
        await _harness.Room.SetAvatarExpressionAsync(
            Player,
            AvatarExpressionType.Idle,
            CancellationToken.None
        );
        _avatar.IsIdle.Should().BeTrue();

        await ActAsync(action);

        _avatar.IsIdle.Should().BeFalse();

        await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(
            _avatar.LastActiveAtMs + 1_000,
            CancellationToken.None
        );

        _avatar.IsIdle.Should().BeFalse();
    }

    private Task<bool> ActAsync(string action) =>
        action switch
        {
            "walk" => _harness.Room.WalkAvatarToAsync(Player, 5, 5, CancellationToken.None),
            "use furniture" => _harness.Room.UseItemByIdAsync(Player, 77, CancellationToken.None),
            "click furniture" => _harness.Room.ClickItemByIdAsync(
                Player,
                77,
                CancellationToken.None
            ),
            _ => _harness.Room.SendChatFromPlayerAsync(
                Player,
                RoomChatType.Chat,
                "hello",
                0,
                CancellationToken.None
            ),
        };
}
