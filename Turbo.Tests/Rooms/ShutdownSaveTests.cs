using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Entities.Furniture;
using Turbo.Main;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What players did just before the server stopped is there when it starts again. A room hands
/// its changes to its persistence grain every two seconds, and handed over the rest as it
/// deactivated; but the silo deactivates rooms only once it has left the cluster, when that
/// call fails. Measured on PostgreSQL, a stop right after 100 rooms had each moved 50 furni lost
/// 4,233 of the 5,000 moves. The server now writes every room before the silo stops.
/// </summary>
public sealed class ShutdownSaveTests : IDisposable
{
    private const int ROOM = 1;
    private const int OWNER = 1;
    private const int ITEM = 40;

    private readonly SqliteDb _db = new();
    private readonly RoomHarness _room = new();
    private readonly CapturingLogger<IRoomService> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ShutdownSaveTests()
    {
        _db.InsertOwnersAndRooms([OWNER], [ROOM]);
        _db.Insert(
            new FurnitureEntity
            {
                Id = ITEM,
                PlayerEntityId = OWNER,
                FurnitureDefinitionEntityId = FurnitureRows.DEFINITION,
                RoomEntityId = ROOM,
                X = 1,
                Y = 1,
            }
        );

        OwnTheRoom();

        // The room's own persistence grain, on the database, as the silo would activate it.
        var persistence = GrainHarness.Create(
            typeof(RoomGrain).Assembly,
            "Turbo.Rooms.Grains.RoomPersistenceGrain",
            _room.Fakes,
            _db
        );
        RoomHarness.SetMember(RoomHarness.GetField(persistence, "_state")!, "RoomId", (RoomId)ROOM);
        _room.Fakes.Instances[(typeof(IRoomPersistenceGrain), (long)ROOM)] = persistence;
        _room.Fakes.Instances[(typeof(IRoomGrain), (long)ROOM)] = _room.Room;
        _room.Fakes.Handlers["GetActiveRoomIdsAsync"] = _ =>
            Task.FromResult(ImmutableArray.Create((RoomId)ROOM));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_furni_moved_just_before_the_server_stops_is_where_it_was_left()
    {
        // As the room loads it: attached, so its changes are queued for the next hand-over.
        (
            await _room.Room.ObjectModule.AttatchObjectAsync(
                _room.CreateFloorItem(ITEM, 1, 1, Altitude.Zero),
                Ct
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.Room.MoveFloorItemByIdAsync(
                ActionContext.CreateForPlayer(OWNER, ROOM),
                ITEM,
                3,
                4,
                Rotation.East,
                Ct
            )
        )
            .Should()
            .BeTrue();

        await Emulator().StopAsync(Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var row = await db.Furnitures.AsNoTracking().SingleAsync(x => x.Id == ITEM, Ct);
        (row.RoomEntityId, row.X, row.Y, row.Rotation).Should().Be((ROOM, 3, 4, Rotation.East));

        // Players were sent away first, so nothing changed while the rooms were written, and
        // the room unloaded once it had been.
        _room
            .Fakes.Log.Calls.Select(x => x.Method)
            .Where(x => x is "StopAsync" or "GetActiveRoomIdsAsync" or "DeactivateOnIdle")
            .Should()
            .Equal("StopAsync", "GetActiveRoomIdsAsync", "DeactivateOnIdle");
        _log.AtLeast(LogLevel.Warning).Should().BeEmpty();
    }

    [Fact]
    public async Task A_room_that_could_not_be_written_is_reported_not_lost_silently()
    {
        _room.Fakes.Instances.TryRemove((typeof(IRoomPersistenceGrain), (long)ROOM), out _);
        _room.Fakes.Handlers["SaveAllAsync"] = _ => Task.FromResult(false);

        await Emulator().StopAsync(Ct);

        _log.AtLeast(LogLevel.Error)
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain("Saved 0 of 1 active rooms")
            .And.Contain("1 could not be written");
    }

    /// <summary>The emulator as the host builds it, with the real room service.</summary>
    private TurboEmulator Emulator()
    {
        var roomService = (IRoomService)
            Activator.CreateInstance(
                typeof(RoomGrain).Assembly.GetType("Turbo.Rooms.RoomService")!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[]
                {
                    _log,
                    Options.Create(new RoomConfig()),
                    _room.Fakes.Create<IGrainFactory>(),
                },
                null
            )!;
        var ctor = typeof(TurboEmulator).GetConstructors().Single();

        return (TurboEmulator)
            ctor.Invoke(
                ctor.GetParameters()
                    .Select(p =>
                        p.ParameterType == typeof(IRoomService) ? roomService
                        : p.ParameterType == typeof(ILogger<TurboEmulator>)
                            ? NullLogger<TurboEmulator>.Instance
                        : _room.Fakes.Create(p.ParameterType)
                    )
                    .ToArray()
            );
    }

    private void OwnTheRoom()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        _room.Fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;

        var info = (RoomSnapshot)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(RoomSnapshot)
            );

        RoomHarness.SetMember(_room.State, "IsRightsLoaded", true);
        RoomHarness.SetMember(info, "RoomId", (RoomId)ROOM);
        RoomHarness.SetMember(info, "OwnerId", (PlayerId)OWNER);
        RoomHarness.SetMember(_room.State, "RoomSnapshot", info);

        // The room tells everyone in it what moved.
        var stream = typeof(RoomGrain).GetField(
            "_roomOutbound",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        stream.SetValue(_room.Room, _room.Fakes.Create(stream.FieldType, "room-stream"));

        // Unloading asks the silo to deactivate the room.
        var grain = typeof(Orleans.Grain);
        var all = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        grain
            .GetProperty("GrainContext", all)!
            .GetSetMethod(true)!
            .Invoke(_room.Room, [_room.Fakes.Create<IGrainContext>("room")]);
        grain
            .GetField("<Runtime>k__BackingField", all)!
            .SetValue(_room.Room, _room.Fakes.Create<IGrainRuntime>("room"));
    }
}
