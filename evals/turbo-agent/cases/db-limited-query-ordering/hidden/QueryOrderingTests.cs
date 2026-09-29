using EvalHarness;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Players.Grains.Messenger;
using Turbo.Primitives.Rooms;
using Xunit;

namespace EvalHidden;

/// <summary>
/// Hidden regression tests: limited queries must be ordered, both so results are deterministic
/// and so EF Core does not warn about a row limit without ORDER BY. The database is EF InMemory
/// with that warning promoted to an error; rows are inserted out of order on purpose.
/// </summary>
public class QueryOrderingTests
{
    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = default,
            PlayerStatus = default,
            PlayerPerks = default,
        };

    [Fact]
    public async Task FriendSearch_ReturnsFirstMatchesInNameOrder()
    {
        var db = new InMemoryDb(throwOnUnorderedTake: true);
        var names = Enumerable.Range(0, 40).Select(i => $"evalname{i:00}").ToList();
        var rng = new Random(7);
        var shuffled = names.OrderBy(_ => rng.Next()).ToList();
        await using (var ctx = db.CreateDbContext())
        {
            var id = 100;
            foreach (var n in shuffled)
                ctx.Players.Add(Player(id++, n));
            ctx.Players.Add(Player(5000, "someoneelse"));
            await ctx.SaveChangesAsync();
        }

        var fakes = new Fakes();
        var grain = (IPlayerMessengerGrain)GrainHarness.Create(
            typeof(Turbo.Players.PlayerModule).Assembly,
            "Turbo.Players.Grains.Messenger.PlayerMessengerGrain",
            fakes,
            db,
            playerId: 1
        );

        var (friends, others) = await grain.SearchPlayersAsync("evalname", CancellationToken.None);

        var limit = new Turbo.Players.Configuration.PlayerConfig().MessengerSearchLimit;
        Assert.Empty(friends);
        Assert.Equal(names.Take(limit).ToList(), others.Select(o => o.Name).ToList());
    }

    [Fact]
    public async Task FriendSearch_SameNamePrefix_TiesAreStable()
    {
        var db = new InMemoryDb(throwOnUnorderedTake: true);
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Players.Add(Player(30, "evalb"));
            ctx.Players.Add(Player(10, "evala"));
            ctx.Players.Add(Player(20, "evalc"));
            await ctx.SaveChangesAsync();
        }
        var grain = (IPlayerMessengerGrain)GrainHarness.Create(
            typeof(Turbo.Players.PlayerModule).Assembly,
            "Turbo.Players.Grains.Messenger.PlayerMessengerGrain",
            new Fakes(),
            db
        );

        var (_, others) = await grain.SearchPlayersAsync("eval", CancellationToken.None);

        Assert.Equal(["evala", "evalb", "evalc"], others.Select(o => o.Name).ToArray());
    }

    [Fact]
    public async Task NavigatorRoomsById_DoNotRelyOnUnorderedLimit()
    {
        using var db = new SqliteDb();
        var owner = Player(1, "owner");
        db.Insert(owner);
        db.Insert(
            new RoomModelEntity
            {
                Id = 1, Name = "m", Model = "00\r00", DoorX = 0, DoorY = 0,
                DoorRotation = default, Enabled = true, Custom = false,
            }
        );
        foreach (var id in new[] { 7, 3, 9, 1 })
            db.Insert(
                new RoomEntity
                {
                    Id = id, Name = $"room{id}", PlayerEntityId = 1, PlayerEntity = owner,
                    DoorMode = default, RoomModelEntityId = 1, RoomModelEntity = null!,
                    UsersNow = 0, PlayersMax = 25, WallHeight = -1, HideWalls = false,
                    ThicknessWall = default, ThicknessFloor = default, AllowBlocking = false,
                    AllowPets = false, AllowPetsEat = false, TradeType = default,
                    MuteType = default, KickType = default, BanType = default,
                    ChatFloodType = default,
                }
            );

        var options = Microsoft.Extensions.Options.Options.Create(new Turbo.Navigator.Configuration.NavigatorConfig());
        using var provider = new Turbo.Navigator.NavigatorProvider(
            db,
            options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Turbo.Navigator.NavigatorProvider>.Instance
        );

        var rooms = await provider.GetRoomsByIdsAsync([(RoomId)9, (RoomId)3, (RoomId)7], CancellationToken.None);

        Assert.Equal([9, 3, 7], rooms.Select(r => (int)r.RoomId).ToArray());
    }
}
