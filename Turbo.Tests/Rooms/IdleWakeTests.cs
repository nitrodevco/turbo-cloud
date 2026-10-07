using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Events;
using Turbo.Events.Registry;
using Turbo.Primitives.Action;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
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
        RoomHarness.SetField(
            _harness.Room,
            "_eventSystem",
            new EventSystem(
                new EventRegistry(_harness.Services, NullLogger<EventRegistry>.Instance)
            )
        );
        RoomHarness.SetField(
            _harness.Room,
            "_roomConfig",
            new RoomConfig { ChatFloodWindowMs = 0, ChatlogEnabled = false }
        );

        var snapshot = RoomHarness.GetMember(_harness.State, "RoomSnapshot")!;
        RoomHarness.SetMember(snapshot, "ChatProtection", ChatFloodSensitivityType.Normal);
        RoomHarness.SetMember(snapshot, "IdleSleepEnabled", true);
        RoomHarness.SetMember(snapshot, "IdleSleepTimeoutSeconds", 60);

        RoomHarness.SetMember(_avatar, "Name", "sleeper");
        _avatar.SetPosition(2, 2);
        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(_harness.State, "AvatarsByObjectId")!
        )[5] = _avatar;
        (
            (IDictionary<PlayerId, RoomObjectId>)
                RoomHarness.GetMember(_harness.State, "AvatarsByPlayerId")!
        )[9] = 5;
        _harness.Module<Turbo.Rooms.Grains.Modules.RoomMapModule>().AddAvatar(_avatar, false);
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
