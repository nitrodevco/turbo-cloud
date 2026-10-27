using FluentAssertions;
using Turbo.Database.Furniture;
using Turbo.Furniture.Custody;
using Turbo.Plugins;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Commands;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The parts of the plugin surface a plugin built on the server's internals before plugins were
/// compiled against the contracts alone: making a room's furni in its own unit of work, reading a
/// file it ships, a box variable's id, and a command acting on the furni beside its executor.
/// </summary>
public sealed class PluginSurfaceTests : IDisposable
{
    private const int OWNER = 8;
    private const int ROOM = 5;
    private const int CHAIR = 1;

    private readonly FurnitureWorld _world = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-files-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _world.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    /// <summary>
    /// A room's furni made in the caller's unit of work (a plugin making a whole room): the rows
    /// stand where they were asked, their ids answered in order, and a rollback leaves none.
    /// </summary>
    [Fact]
    public async Task Furni_made_into_a_room_are_the_callers_to_commit_or_roll_back()
    {
        _world.AddPlayer(OWNER, "owner");
        _world.AddDefinition(CHAIR, "chair", "default_floor");
        _world.AddRoom(ROOM, OWNER);
        IRoomFurniWriter writer = new RoomFurniWriter(TestCustody.Over(_world.Db));
        NewRoomFurni[] furni =
        [
            new(CHAIR, null, new RoomPosition(1, 1, 0, Rotation.South, null)),
            new(CHAIR, "{\"state\":\"1\"}", new RoomPosition(0, 1, 0.5, Rotation.East, null)),
        ];

        await using (var db = await _world.Db.CreateDbContextAsync(Ct))
        {
            await using var tx = await db.Database.BeginTransactionAsync(Ct);

            var ids = await writer.CreateInRoomAsync(
                db,
                ROOM,
                OWNER,
                furni,
                TestCustody.Cause(),
                Ct
            );

            await tx.CommitAsync(Ct);

            _world
                .Rows()
                .Select(x => (x.Id, x.RoomEntityId, x.X, x.Y, x.Rotation))
                .Should()
                .Equal((ids[0], ROOM, 1, 1, Rotation.South), (ids[1], ROOM, 0, 1, Rotation.East));
        }

        await using (var db = await _world.Db.CreateDbContextAsync(Ct))
        {
            await using var tx = await db.Database.BeginTransactionAsync(Ct);

            await writer.CreateInRoomAsync(db, ROOM, OWNER, furni, TestCustody.Cause(), Ct);
            await tx.RollbackAsync(Ct);
        }

        _world.Rows().Should().HaveCount(2, "the rolled back write left nothing");
    }

    [Fact]
    public void A_plugin_reads_the_files_of_its_own_folder_and_no_other()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "data"));
        File.WriteAllText(Path.Combine(_folder, "data", "rooms.json"), "{}");
        File.WriteAllText(_folder + "-secret.txt", "not the plugin's");
        var files = new PluginFiles(_folder);

        using (var stream = files.OpenRead("data/rooms.json"))
            new StreamReader(stream!).ReadToEnd().Should().Be("{}");

        files.OpenRead("missing.json").Should().BeNull();
        files
            .Invoking(x => x.OpenRead("../" + Path.GetFileName(_folder) + "-secret.txt"))
            .Should()
            .Throw<ArgumentException>();

        File.Delete(_folder + "-secret.txt");
    }

    /// <summary>
    /// The id of the variable a box makes is saved with the room and named by the box's id on
    /// every server: it must never change. The values are the database source over the low 48
    /// bits of XXH64 (seed 0) of the box id's big-endian bytes, worked out apart from .NET's.
    /// </summary>
    [Fact]
    public void A_box_variables_id_is_the_same_as_ever()
    {
        WiredVariableId.OfBox(1).Value.Should().Be(BOX_1);
        WiredVariableId.OfBox(1, 0).Value.Should().Be(BOX_1_SUB_0);
    }

    private const ulong BOX_1 = 794574929339797UL;
    private const ulong BOX_1_SUB_0 = 566182212888151UL;

    /// <summary>
    /// <c>:rolldice</c>'s needs: the floor furni on a tile beside the executor, lowest first, and a
    /// line said as the executor that the whole room hears.
    /// </summary>
    [Fact]
    public async Task A_command_reads_the_furni_on_a_tile_and_speaks_as_its_executor()
    {
        var room = new WiredRoom(6, 6);
        var alice = room.Enter(1, 2, 2, "alice");
        var bottom = room.AddFloorItem(10, 3, 2);
        var top = room.AddFloorItem(11, 3, 2);
        top.SetPositionZ(Turbo.Primitives.Rooms.Object.Altitude.FromInt(100));
        var scope = new RoomCommandScope(room.Harness.Room, room.ContextFor(1));

        scope.FloorItemsAt(3, 2).Select(x => x.ObjectId.Value).Should().Equal(10, 11);
        scope.FloorItemsAt(9, 9).Should().BeEmpty("off the map");

        await scope.SayAsync(alice, "7 + 4 = 11", true, Ct);

        var shout = room
            .Harness.Fakes.SentToRoom<ShoutMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;
        (shout.ObjectId.Value, shout.Text).Should().Be((1, "7 + 4 = 11"));
    }
}
