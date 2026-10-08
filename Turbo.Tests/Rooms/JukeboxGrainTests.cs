using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Furniture.Providers;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Sound;
using Turbo.Primitives.Sound.Enums;
using Turbo.Primitives.Sound.Grains;
using Turbo.Primitives.Sound.Snapshots;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A jukebox keeps the song disks put into it as the same rows (still their owner's, in no
/// inventory) in the playlist's order, gives a disk back to whoever put it in, and takes only
/// song disks a player holds, of songs the hotel has, while it has room.
/// </summary>
public sealed class JukeboxGrainTests : IDisposable
{
    private const int OWNER = 1;
    private const int ALICE = 2;
    private const int JUKEBOX = 100;

    private const int DISK_DEFINITION = 1;
    private const int CHAIR_DEFINITION = 2;

    private const int SONG = 5;
    private const int OTHER_SONG = 6;
    private const int MISSING_SONG = 7;

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly Dictionary<int, FurnitureDefinitionSnapshot> _definitions = new()
    {
        [DISK_DEFINITION] = Definition(DISK_DEFINITION, "song_disk", FurnitureCategory.TraxSong),
        [CHAIR_DEFINITION] = Definition(CHAIR_DEFINITION, "chair", FurnitureCategory.Default),
    };
    private readonly IInventoryFurnitureLoader _loader;

    public JukeboxGrainTests()
    {
        _db.Insert(Player(OWNER, "owner"));
        _db.Insert(Player(ALICE, "alice"));

        foreach (var definition in _definitions.Values)
            _db.Insert(DefinitionEntity(definition));

        _db.Insert(Furni(JUKEBOX, CHAIR_DEFINITION, OWNER, extraData: null));

        _fakes.Handlers["TryGetDefinition"] = call =>
            call.Args[0] is int id ? _definitions.GetValueOrDefault(id) : Fakes.NotHandled;

        // The inventory hands out what the player holds: their rows in no room and no holder.
        _fakes.Handlers["GetItemSnapshotsAsync"] = call =>
            Task.FromResult(
                Inventory(Convert.ToInt32(call.Key), (ImmutableArray<RoomObjectId>)call.Args[0]!)
            );

        // The hotel has two songs.
        _fakes.Handlers["GetSongsAsync"] = call =>
            Task.FromResult<ImmutableArray<SongSnapshot>>([
                .. ((ImmutableArray<int>)call.Args[0]!)
                    .Where(id => id is SONG or OTHER_SONG)
                    .Select(Song),
            ]);

        _loader = (IInventoryFurnitureLoader)
            Activator.CreateInstance(
                typeof(Turbo.Inventory.InventoryModule).Assembly.GetType(
                    "Turbo.Inventory.Factories.InventoryFurnitureLoader"
                )!,
                All,
                null,
                [
                    _db,
                    _fakes.Create<IFurnitureDefinitionProvider>(),
                    new StuffDataFactory(NullLogger<IStuffDataFactory>.Instance),
                    NullLogger<IInventoryFurnitureLoader>.Instance,
                ],
                null
            )!;
    }

    public void Dispose() => _db.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_disk_put_in_is_held_by_the_jukebox_and_leaves_the_inventory()
    {
        _db.Insert(Furni(10, DISK_DEFINITION, ALICE, SONG));
        var jukebox = await JukeboxAsync();

        var result = await jukebox.AddDiskAsync(ALICE, 10, 0, Ct);

        result.Result.Should().Be(JukeboxChangeResultType.Done);
        result.Disks.Should().Equal(new SongDiskSnapshot { DiskId = 10, SongId = SONG });
        var row = Rows(10).Single();
        row.ChestItemEntityId.Should().Be(JUKEBOX);
        row.HeldPosition.Should().Be(0);
        row.PlayerEntityId.Should().Be(ALICE);
        Released(ALICE).Should().Equal((RoomObjectId)10);
        (await _loader.LoadByPlayerIdAsync(ALICE, "alice", Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_playlist_keeps_the_order_disks_were_put_in_at_across_reloads()
    {
        _db.Insert(Furni(10, DISK_DEFINITION, OWNER, SONG));
        _db.Insert(Furni(11, DISK_DEFINITION, OWNER, OTHER_SONG));
        _db.Insert(Furni(12, DISK_DEFINITION, OWNER, SONG));
        var jukebox = await JukeboxAsync();

        await jukebox.AddDiskAsync(OWNER, 10, 0, Ct);
        await jukebox.AddDiskAsync(OWNER, 11, 99, Ct); // past the end: at the end
        await jukebox.AddDiskAsync(OWNER, 12, 0, Ct); // at the top

        (await (await JukeboxAsync()).GetDisksAsync(Ct))
            .Select(x => x.DiskId.Value)
            .Should()
            .Equal(12, 10, 11);
    }

    [Fact]
    public async Task A_full_jukebox_takes_no_more_disks()
    {
        _db.Insert(Furni(10, DISK_DEFINITION, OWNER, SONG));
        _db.Insert(Furni(11, DISK_DEFINITION, OWNER, SONG));
        var jukebox = await JukeboxAsync(maxDisks: 1);
        await jukebox.AddDiskAsync(OWNER, 10, 0, Ct);

        var result = await jukebox.AddDiskAsync(OWNER, 11, 1, Ct);

        result.Result.Should().Be(JukeboxChangeResultType.Full);
        result.Disks.Should().ContainSingle();
        Rows(11).Single().ChestItemEntityId.Should().BeNull();
    }

    [Fact]
    public async Task Only_a_song_disk_the_player_holds_with_a_song_the_hotel_has_goes_in()
    {
        _db.Insert(Furni(10, DISK_DEFINITION, ALICE, SONG)); // Alice's, not the owner's
        _db.Insert(Furni(11, CHAIR_DEFINITION, OWNER, extraData: null));
        _db.Insert(Furni(12, DISK_DEFINITION, OWNER, MISSING_SONG));
        var jukebox = await JukeboxAsync();

        (await jukebox.AddDiskAsync(OWNER, 10, 0, Ct))
            .Result.Should()
            .Be(JukeboxChangeResultType.DiskNotHeld);
        (await jukebox.AddDiskAsync(OWNER, 11, 0, Ct))
            .Result.Should()
            .Be(JukeboxChangeResultType.NotASongDisk);
        (await jukebox.AddDiskAsync(OWNER, 12, 0, Ct))
            .Result.Should()
            .Be(JukeboxChangeResultType.UnknownSong);

        Rows(10, 11, 12).Should().AllSatisfy(row => row.ChestItemEntityId.Should().BeNull());
        _fakes.Log.Of("ReleaseFurnitureAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task A_disk_taken_out_goes_back_to_whoever_put_it_in()
    {
        _db.Insert(Furni(10, DISK_DEFINITION, ALICE, SONG));
        _db.Insert(Furni(11, DISK_DEFINITION, OWNER, OTHER_SONG));
        var jukebox = await JukeboxAsync();
        await jukebox.AddDiskAsync(ALICE, 10, 0, Ct);
        await jukebox.AddDiskAsync(OWNER, 11, 1, Ct);

        var result = await jukebox.RemoveDiskAsync(0, Ct);

        result.Result.Should().Be(JukeboxChangeResultType.Done);
        result.Disks.Should().Equal(new SongDiskSnapshot { DiskId = 11, SongId = OTHER_SONG });
        var returned = Rows(10).Single();
        returned.ChestItemEntityId.Should().BeNull();
        returned.HeldPosition.Should().BeNull();
        returned.PlayerEntityId.Should().Be(ALICE);
        Rows(11).Single().HeldPosition.Should().Be(0);
        var receive = _fakes.Log.Of("ReceiveFurnitureAsync").Single();
        receive.Key.Should().Be((long)ALICE);
        ((ImmutableArray<FurnitureItemSnapshot>)receive.Args[0]!)
            .Single()
            .ItemId.Should()
            .Be((RoomObjectId)10);
        (await jukebox.RemoveDiskAsync(5, Ct))
            .Result.Should()
            .Be(JukeboxChangeResultType.NoSuchSlot);
    }

    private async Task<IJukeboxGrain> JukeboxAsync(int maxDisks = 10)
    {
        var grain = GrainHarness.Create(
            typeof(RoomGrain).Assembly,
            "Turbo.Rooms.Grains.Jukebox.JukeboxGrain",
            _fakes,
            _db
        );

        RoomHarness.SetMember(
            RoomHarness.GetField(grain, "_state")!,
            "JukeboxId",
            (RoomObjectId)JUKEBOX
        );
        RoomHarness.SetField(grain, "_roomConfig", new RoomConfig { JukeboxMaxDisks = maxDisks });
        RoomHarness.SetField(grain, "_defsProvider", _fakes.Create<IFurnitureDefinitionProvider>());
        RoomHarness.SetField(grain, "_furnitureLoader", _loader);

        await ((Grain)grain).OnActivateAsync(Ct);

        return (IJukeboxGrain)grain;
    }

    private IEnumerable<RoomObjectId> Released(int playerId) =>
        _fakes
            .Log.Of("ReleaseFurnitureAsync")
            .Where(call => Equals(call.Key, (long)playerId))
            .SelectMany(call => (ImmutableArray<RoomObjectId>)call.Args[0]!);

    private List<FurnitureEntity> Rows(params int[] ids)
    {
        using var ctx = _db.CreateDbContext();

        return [.. ctx.Furnitures.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id)];
    }

    private ImmutableArray<FurnitureItemSnapshot> Inventory(
        int playerId,
        ImmutableArray<RoomObjectId> ids
    )
    {
        using var ctx = _db.CreateDbContext();
        var wanted = ids.Select(x => x.Value).ToList();

        return
        [
            .. ctx
                .Furnitures.AsNoTracking()
                .Where(x =>
                    wanted.Contains(x.Id)
                    && x.PlayerEntityId == playerId
                    && x.RoomEntityId == null
                    && x.ChestItemEntityId == null
                )
                .OrderBy(x => x.Id)
                .AsEnumerable()
                .Select(x =>
                    _loader
                        .Create(
                            x.Id,
                            x.PlayerEntityId,
                            "player",
                            _definitions[x.FurnitureDefinitionEntityId],
                            x.ExtraData,
                            x.CreatedAt
                        )
                        .GetSnapshot()
                ),
        ];
    }

    private static SongSnapshot Song(int id) =>
        new()
        {
            Id = id,
            Code = string.Empty,
            Name = $"song {id}",
            Author = "staff",
            Track = "1:0,4",
            LengthSeconds = 60,
            IsOfficial = true,
        };

    private static FurnitureDefinitionSnapshot Definition(
        int id,
        string name,
        FurnitureCategory category
    ) =>
        new()
        {
            Id = id,
            SpriteId = 1000 + id,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = category,
            LogicName = category == FurnitureCategory.TraxSong ? SongDisks.LOGIC : "default_floor",
            TotalStates = 0,
            Width = 1,
            Length = 1,
            StackHeight = Altitude.FromInt(100),
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            UsagePolicy = FurnitureUsageType.Everybody,
            ExtraData = null,
        };

    private static FurnitureDefinitionEntity DefinitionEntity(
        FurnitureDefinitionSnapshot definition
    ) =>
        new()
        {
            Id = definition.Id,
            SpriteId = definition.SpriteId,
            Name = definition.Name,
            ProductType = definition.ProductType,
            FurniCategory = definition.FurniCategory,
            Logic = definition.LogicName,
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
        };

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private static FurnitureEntity Furni(int id, int definitionId, int ownerId, int songId) =>
        Furni(
            id,
            definitionId,
            ownerId,
            ProductStuffData.ExtraData(songId.ToString(CultureInfo.InvariantCulture))
        );

    private static FurnitureEntity Furni(
        int id,
        int definitionId,
        int ownerId,
        string? extraData
    ) =>
        new()
        {
            Id = id,
            PlayerEntityId = ownerId,
            FurnitureDefinitionEntityId = definitionId,
            ExtraData = extraData,
        };
}
