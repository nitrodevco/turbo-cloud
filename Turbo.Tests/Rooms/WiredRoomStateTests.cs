using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Commands;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The wired menu's room state: Reload and Rollback in the settings tab, <c>:reload</c> and
/// <c>:rollback</c> in chat, all sending <c>WiredUpdateRoomComposer(rollback)</c>
/// (<c>WiredMenuSettingsTab.as</c> 82/89, <c>ChatInputWidgetHandler.as</c> 558/567,
/// WIN63-202610151745). Reload is enabled for whoever may modify the room's wired, Rollback for
/// the owner or staff (<c>WiredMenuSettingsTab.updateButtonsUI</c>). Texts: "Everyone will
/// re-enter the room"; "Any furni movement or state change since the last room reload will be
/// gone!".
/// </summary>
public sealed class WiredRoomStateTests
{
    private const int OWNER = 1;
    private const int RIGHTS = 2;
    private const int VISITOR = 3;

    private readonly CommandRoomFixture _room = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredRoomStateTests()
    {
        _room.AddPlayer(OWNER);
        _room.AddPlayer(RIGHTS);
        _room.AddPlayer(VISITOR);
        _room.GiveRights(RIGHTS);

        RoomHarness.SetMember(
            RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!,
            "WiredModifyPermissionMask",
            WiredPermissionFlags.Rights
        );
    }

    private Task<ImmutableArray<PlayerId>> UpdateAsync(int playerId, bool rollback) =>
        _room.Harness.Room.UpdateWiredRoomStateAsync(
            ActionContext.CreateForPlayer(playerId, _room.Harness.Room.RoomId),
            rollback,
            Ct
        );

    private IEnumerable<FakeCall> PersistenceCalls =>
        _room.Harness.Fakes.Log.On<IRoomPersistenceGrain>();

    [Fact]
    public async Task Reload_by_a_wired_modifier_writes_the_room_then_sends_everyone_out()
    {
        var sentOut = await UpdateAsync(RIGHTS, rollback: false);

        sentOut.Should().BeEquivalentTo(new PlayerId[] { OWNER, RIGHTS, VISITOR });
        new[] { OWNER, RIGHTS, VISITOR }.Select(_room.IsInRoom).Should().OnlyContain(x => !x);
        PersistenceCalls.Select(x => x.Method).Should().Contain("FlushNowAsync");
    }

    [Fact]
    public async Task Reload_by_someone_who_may_not_modify_wired_does_nothing()
    {
        var sentOut = await UpdateAsync(VISITOR, rollback: false);

        sentOut.Should().BeEmpty();
        _room.IsInRoom(VISITOR).Should().BeTrue();
        PersistenceCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Rollback_is_for_the_owner_only_not_for_a_wired_modifier()
    {
        var sentOut = await UpdateAsync(RIGHTS, rollback: true);

        sentOut.Should().BeEmpty();
        _room.IsInRoom(RIGHTS).Should().BeTrue();
        PersistenceCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Rollback_puts_moved_and_switched_furni_back_as_loaded_before_the_write()
    {
        var builder = new RoomHarness();
        var item = builder.CreateFloorItem(50, 1, 1, 0);
        item.SetExtraData("""{"state":"0"}""");
        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!)[item.ObjectId] =
            item;
        // What the furni load does once the room has read its furni.
        typeof(RoomFurniModule)
            .GetMethod("CaptureLoadedItems", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_room.Harness.Module<RoomFurniModule>(), []);

        var loadedData = item.GetSnapshot().ExtraData;

        item.SetPosition(3, 4);
        item.SetExtraData("""{"state":"1"}""");

        var sentOut = await UpdateAsync(OWNER, rollback: true);

        sentOut.Should().HaveCount(3);
        var calls = PersistenceCalls.ToList();
        var enqueue = calls.FindIndex(x => x.Method == "EnqueueDirtyItemsAsync");
        var flush = calls.FindIndex(x => x.Method == "FlushNowAsync");
        enqueue.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(flush);
        calls
            .Where(x => x.Method == "EnqueueDirtyItemsAsync")
            .SelectMany(x => (List<RoomItemSnapshot>)x.Args[1]!)
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<RoomItemSnapshot>(x => x.X == 1 && x.Y == 1 && x.ExtraData == loadedData);
    }

    [Fact]
    public void The_client_flag_is_read_as_rollback()
    {
        var header = PacketHarness.Incoming("WiredUpdateRoomMessageEvent");

        PacketHarness
            .Parse(header, PacketHarness.Payload(w => w.Bool(true)))
            .Should()
            .BeOfType<WiredUpdateRoomMessage>()
            .Which.Rollback.Should()
            .BeTrue();
    }

    [Fact]
    public async Task Everyone_sent_out_has_their_session_closed_then_is_sent_back_in()
    {
        var packets = new PacketHarness();
        packets.Fakes.Handlers["UpdateWiredRoomStateAsync"] = _ =>
            Task.FromResult(ImmutableArray.Create<PlayerId>(OWNER, VISITOR));

        await packets.SendAsync(
            PacketHarness.Incoming("WiredUpdateRoomMessageEvent"),
            PacketHarness.Payload(w => w.Bool(false)),
            playerId: OWNER,
            roomId: 7
        );

        packets
            .Fakes.Log.Calls.Where(x =>
                x.Method is "OnRemovedFromRoomAsync" or "ForwardToRoomAsync"
            )
            .Select(x => $"{x.Method} {x.Key} {x.Args[0]}")
            .Should()
            .Equal(
                $"OnRemovedFromRoomAsync {OWNER} 7",
                $"ForwardToRoomAsync {OWNER} 7",
                $"OnRemovedFromRoomAsync {VISITOR} 7",
                $"ForwardToRoomAsync {VISITOR} 7"
            );
    }
}
