using System.Collections.Immutable;
using System.Diagnostics;
using FluentAssertions;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A saved floor plan reloads the room the way Habbo's does. Official client capture
/// (2026-10-17, six saves through the editor with a second player in the room): ~100 ms after
/// <c>UpdateFloorProperties</c> each client gets <c>UserRemove</c> and <c>CloseConnection</c>, no
/// notification, and ~2.0 s later <c>RoomForward</c> to the same room; both then come in on the
/// door tile facing the new direction, with object ids restarted. Before, the room was streamed
/// again in place to whoever stood in it, and a player could keep seeing what the room held
/// from the old plan (Live 2026-10-17: the saved Enter direction applied only after an unload).
/// </summary>
public sealed class FloorPlanReloadTests
{
    private const int OWNER = 1;
    private const int VISITOR = 2;
    private const int ROOM = 7;

    [Fact]
    public async Task Everyone_in_the_room_is_closed_without_a_notice_then_sent_back_in_two_seconds_later()
    {
        var packets = new PacketHarness();
        packets.Fakes.Handlers["SaveFloorPlanAsync"] = _ =>
            Task.FromResult<ImmutableArray<PlayerId>?>(
                ImmutableArray.Create<PlayerId>(OWNER, VISITOR)
            );
        var stamps = new List<(string Call, long At)>();
        var clock = Stopwatch.StartNew();
        packets.Fakes.Handlers["OnRemovedFromRoomAsync"] = _ =>
        {
            stamps.Add(("close", clock.ElapsedMilliseconds));
            return Task.CompletedTask;
        };
        packets.Fakes.Handlers["ForwardToRoomAsync"] = _ =>
        {
            stamps.Add(("forward", clock.ElapsedMilliseconds));
            return Task.CompletedTask;
        };

        await packets.SendAsync(
            PacketHarness.Incoming("UpdateFloorPropertiesMessageEvent"),
            PacketHarness.Payload(w =>
                w.String("000\r000\r000").Int(1).Int(1).Int(6).Int(0).Int(0)
            ),
            playerId: OWNER,
            roomId: ROOM
        );

        packets
            .Fakes.Log.Calls.Where(x =>
                x.Method
                    is "SendComposerAsync"
                        or "OnRemovedFromRoomAsync"
                        or "ForwardToRoomAsync"
                        or "GetEntryViewAsync"
            )
            .Select(x =>
                x.Method == "OnRemovedFromRoomAsync"
                    ? $"{x.Method} {x.Key} {x.Args[0]} kicked {x.Args[1]}"
                    : $"{x.Method} {x.Key} {x.Args[0]}"
            )
            .Should()
            .Equal(
                $"OnRemovedFromRoomAsync {OWNER} {ROOM} kicked False",
                $"OnRemovedFromRoomAsync {VISITOR} {ROOM} kicked False",
                $"ForwardToRoomAsync {OWNER} {ROOM}",
                $"ForwardToRoomAsync {VISITOR} {ROOM}"
            );
        (stamps.First(x => x.Call == "forward").At - stamps.Last(x => x.Call == "close").At)
            .Should()
            .BeGreaterThanOrEqualTo(1900);
    }

    [Fact]
    public async Task A_refused_save_sends_nobody_anywhere()
    {
        var packets = new PacketHarness();
        packets.Fakes.Handlers["SaveFloorPlanAsync"] = _ =>
            Task.FromResult<ImmutableArray<PlayerId>?>(null);

        await packets.SendAsync(
            PacketHarness.Incoming("UpdateFloorPropertiesMessageEvent"),
            PacketHarness.Payload(w =>
                w.String("000\r000\r000").Int(1).Int(1).Int(6).Int(0).Int(0)
            ),
            playerId: OWNER,
            roomId: ROOM
        );

        packets
            .Fakes.Log.Calls.Select(x => x.Method)
            .Should()
            .NotContain(["OnRemovedFromRoomAsync", "ForwardToRoomAsync", "GetEntryViewAsync"]);
    }
}
