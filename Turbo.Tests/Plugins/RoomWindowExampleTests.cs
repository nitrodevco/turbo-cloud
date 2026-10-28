using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Gamedata.ClientModules;
using Turbo.Gamedata.Configuration;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Watching;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Plugins;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The example in <c>docs/examples/room-window</c> is the documentation's proof of the room hooks:
/// it is compiled from its sources and loaded as a hotel loads it, and its window logic runs in a
/// live room (the far half in another room, as the database says). Its handlers and arrival policy
/// have the example's own tests (<c>docs/examples/room-window/tests</c>, compiled into this project).
/// </summary>
public sealed class RoomWindowExampleTests : IDisposable
{
    private const int ALICE = 5;
    private const int ALICE_PLAYER = 100 + ALICE;
    private const int WINDOW = 10;
    private const int PARTNER = 11;
    private static readonly RoomId FAR_ROOM = new(77);

    private readonly WiredRoom _room = new(10, 10);
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-room-window-example-" + Guid.NewGuid().ToString("N")
    );
    private IDisposable? _plugin;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Example =>
        Path.Combine(RepositoryRoot(), "docs", "examples", "room-window");

    public void Dispose()
    {
        _plugin?.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task The_example_compiles_from_its_sources_and_loads_with_its_client_module()
    {
        CopyExample(Path.Combine(_folder, "roomwindow"));

        var registry = new ClientModuleRegistry(
            new TestOptionsMonitor<GamedataConfig>(new GamedataConfig()),
            NullLogger<ClientModuleRegistry>.Instance
        );
        var manager = new PluginManager(
            new ServiceCollection()
                .AddSingleton<IClientModuleDirectory>(registry)
                .BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);
        registry.Modules.Should().ContainSingle().Which.Key.Should().Be("roomwindow");
        (await File.ReadAllTextAsync(registry.FindFile("roomwindow", "roomwindow.js")!, Ct))
            .Should()
            .Contain("roomwindow.cross", "the server built the module from the example's source");

        await manager.UnloadAllAsync(Ct);
    }

    /// <summary>
    /// The window on (5, 2) faces south, so the tile behind it is (5, 1); the other half answers
    /// where it stands in its own room. Watching passes the far room's door only because it is the
    /// window's own pair (core's check).
    /// </summary>
    [Fact]
    public async Task A_window_says_where_it_looks_and_lets_the_player_watch_its_pairs_room()
    {
        await PlacePartnerAsync();
        _room.Enter(ALICE, 2, 7, "alice");
        await PlaceWindowAsync();

        (await AskAsync("look"))
            .Payload.Should()
            .Be(
                $$"""{"PartnerId":{{PARTNER}},"RoomId":{{FAR_ROOM.Value}},"BehindX":5,"BehindY":1}"""
            );
        (await AskAsync("where")).Payload.Should().Be("""{"X":5,"Y":2}""");
        (await AskAsync("watch")).IsHandled.Should().BeTrue();

        var watch = _room
            .Harness.Fakes.Log.Of(nameof(IPluginRoomLeases.WatchAsync))
            .Should()
            .ContainSingle()
            .Subject.Args;
        watch[0].Should().Be("roomwindow");
        watch[2].Should().Be(FAR_ROOM);
        ((Turbo.Primitives.Plugins.Messages.PluginMessageComposer)watch[3]!)
            .Name.Should()
            .Be("roomwindow.room");
        watch[5].Should().Be(true, "the window's own pair passes the far room's door");
    }

    /// <summary>
    /// A cross walks the player to the tile in front of the window, then forwards them through
    /// its pair (passing the door) with the clicked tile for the far room's arrival policy.
    /// </summary>
    [Fact]
    public async Task A_cross_walks_the_player_to_the_window_and_sends_them_through_with_the_tile()
    {
        await PlacePartnerAsync();
        var alice = _room.Enter(ALICE, 2, 7, "alice");
        await PlaceWindowAsync();

        (await AskAsync("cross", 3, 4)).Payload.Should().Be("true");
        Forwards().Should().BeEmpty("she is not at the window yet");

        await TickAsync(12);

        (alice.X, alice.Y).Should().Be((5, 3), "the tile in front of the window");
        var entry = (RoomEntrySnapshot)Forwards().Should().ContainSingle().Subject.Args[1]!;
        entry.ThroughLinkedPair.Should().BeTrue();
        entry.Method.Should().Be(RoomEntryMethodType.Default);
        entry.Extension.Should().Be(new RoomEntryExtension("roomwindow", """{"X":3,"Y":4}"""));
    }

    private Task<FurniLogicReply> AskAsync(string what, int x = 0, int y = 0) =>
        _room.Harness.Room.InvokeFurniLogicAsync(
            WINDOW,
            FurniLogicMessage.Of(
                "room_window",
                new
                {
                    Do = what,
                    X = x,
                    Y = y,
                },
                ALICE_PLAYER
            ),
            Ct
        );

    private IEnumerable<FakeCall> Forwards() => _room.Harness.Fakes.Log.Of("ForwardToRoomAsync");

    // The window's other half, in the far room as the database says.
    private async Task PlacePartnerAsync()
    {
        var db = new InMemoryDb();

        await using (var dbCtx = await db.CreateDbContextAsync(Ct))
        {
            dbCtx.Furnitures.Add(
                new Turbo.Database.Entities.Furniture.FurnitureEntity
                {
                    Id = PARTNER,
                    PlayerEntityId = ALICE_PLAYER,
                    FurnitureDefinitionEntityId = 1,
                    RoomEntityId = FAR_ROOM.Value,
                }
            );
            await dbCtx.SaveChangesAsync(Ct);
        }

        _room.Harness.Room.UseDatabase(db);
    }

    private async Task PlaceWindowAsync()
    {
        var plugin = Assembly.Load(
            PluginBuilder.Compile(
                "roomwindow",
                [
                    .. Directory
                        .EnumerateFiles(Path.Combine(Example, "server"), "*.cs")
                        .Order()
                        .Select(File.ReadAllText),
                ]
            )
        );
        // The plugin's own services, as a loaded plugin's: its manifest and its directory.
        var services = new ServiceCollection()
            .AddSingleton(
                new PluginManifest
                {
                    Name = "Room window",
                    Key = "roomwindow",
                    Version = "1.0.0",
                    Author = "test",
                }
            )
            .AddSingleton(plugin.GetType("RoomWindow.WindowDirectory", throwOnError: true)!)
            .BuildServiceProvider();

        _plugin = await new PluginLogicFeatureProcessor(_room.Harness.LogicProvider).ProcessAsync(
            plugin,
            new CompositeServiceProvider(services, _room.Harness.Services),
            Ct
        );

        var window = _room.AddFloorItem(
            WINDOW,
            5,
            2,
            "room_window",
            canWalk: false,
            createLogic: (_, ctx) =>
                _room.Harness.LogicProvider.CreateLogicInstance("room_window", ctx)
        );
        window.SetRotation(Rotation.South);
        window.ExtraData.UpdateSection(
            RoomLinkerData.SECTION,
            new RoomLinkerData { ItemId = PARTNER }
        );
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _room.Clock.AdvanceMs(500);

            var now = _room.Harness.NowMs();

            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(now, Ct);
            await _room.Harness.Module<RoomTimerSystem>().ProcessTimersAsync(now, Ct);
        }
    }

    private static void CopyExample(string target)
    {
        foreach (
            var file in new[] { "manifest.json" }
                .Concat(
                    Directory
                        .EnumerateFiles(Path.Combine(Example, "server"), "*.cs")
                        .Select(x => Path.Combine("server", Path.GetFileName(x)))
                )
                .Concat(
                    Directory
                        .EnumerateFiles(Path.Combine(Example, "client", "src"))
                        .Select(x => Path.Combine("client", "src", Path.GetFileName(x)))
                )
        )
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(target, file))!);
            File.Copy(Path.Combine(Example, file), Path.Combine(target, file));
        }
    }

    private static string RepositoryRoot()
    {
        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
            if (File.Exists(Path.Combine(dir.FullName, "Turbo.Cloud.sln")))
                return dir.FullName;

        throw new InvalidOperationException("Turbo.Cloud.sln was not found above the tests.");
    }
}
