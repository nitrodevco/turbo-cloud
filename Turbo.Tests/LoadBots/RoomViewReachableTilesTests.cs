using FluentAssertions;
using Turbo.Database.Entities.Furniture;
using Turbo.LoadBots.Client;
using Turbo.LoadBots.Knowledge;
using Turbo.LoadBots.Protocol.Decoders;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.LoadBots;

/// <summary>
/// The bot only walks to tiles the server would find a path to, so a walk that fails is the
/// server's doing. These follow the server's rules in <c>RoomMapModule.CanAvatarWalk</c>.
/// </summary>
public sealed class RoomViewReachableTilesTests
{
    private const int SELF_PLAYER = 1;
    private const int SELF = 10;
    private const int SOLID = 100;
    private const int SEAT = 101;
    private const int RUG = 102;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnOpenRoomIsReachableEverywhereButTheBotsOwnTile()
    {
        var room = Room(5, 5, at: (0, 0));

        var tiles = room.ReachableTiles(await KnowledgeAsync(), SELF);

        tiles.Should().HaveCount(24).And.NotContain((0, 0));
    }

    [Fact]
    public async Task ATileWalledInBySolidFurniIsNotReachable()
    {
        // A ring of solid furni round (3,3); the bot stands outside it.
        var room = Room(7, 7, at: (0, 0));
        var id = 1000;
        room.SetItems(
            from x in Enumerable.Range(2, 3)
            from y in Enumerable.Range(2, 3)
            where (x, y) != (3, 3)
            select Item(id++, SOLID, x, y)
        );

        var tiles = room.ReachableTiles(await KnowledgeAsync(), SELF);

        tiles.Should().NotContain((3, 3)).And.NotContain((2, 2)).And.Contain((6, 6));
    }

    [Fact]
    public async Task ASeatEndsAWalkButIsNeverWalkedThrough()
    {
        // A corridor one tile wide, blocked halfway by a chair.
        var room = Room(5, 1, at: (0, 0));
        room.SetItems([Item(1000, SEAT, 2, 0)]);

        var tiles = room.ReachableTiles(await KnowledgeAsync(), SELF);

        tiles.Should().BeEquivalentTo([(1, 0), (2, 0)]);
    }

    [Fact]
    public async Task WalkableFurniIsWalkedOver()
    {
        var room = Room(5, 1, at: (0, 0));
        room.SetItems([Item(1000, RUG, 2, 0)]);

        var tiles = room.ReachableTiles(await KnowledgeAsync(), SELF);

        tiles.Should().BeEquivalentTo([(1, 0), (2, 0), (3, 0), (4, 0)]);
    }

    [Fact]
    public async Task ADiagonalStepBetweenTwoSolidCornersIsRefused()
    {
        // From (0,0) the only way on is the diagonal to (1,1), and both its sides are solid.
        var room = Room(3, 3, at: (0, 0));
        room.SetItems([
            Item(1000, SOLID, 1, 0),
            Item(1001, SOLID, 0, 1),
            Item(1002, SOLID, 2, 0),
            Item(1003, SOLID, 0, 2),
        ]);

        room.ReachableTiles(await KnowledgeAsync(), SELF).Should().BeEmpty();
    }

    [Fact]
    public async Task AnotherAvatarsTileIsPassedButNotAGoal()
    {
        var room = Room(5, 1, at: (0, 0));
        room.AddAvatars([Avatar(2, 11, 2, 0)]);

        var tiles = room.ReachableTiles(await KnowledgeAsync(), SELF);

        tiles.Should().BeEquivalentTo([(1, 0), (3, 0), (4, 0)]);
    }

    [Fact]
    public async Task TheSearchStartsWhereTheBotHasWalkedTo()
    {
        // The avatar entered at (0,0) but has since walked into a pocket only (4,0) opens onto.
        var room = Room(5, 2, at: (0, 0));
        room.SetItems([Item(1000, SOLID, 3, 0), Item(1001, SOLID, 3, 1), Item(1002, SOLID, 4, 1)]);
        room.UpdateStatuses([new AvatarStatus(SELF, 4, 0, 0, "/")]);

        room.ReachableTiles(await KnowledgeAsync(), SELF).Should().BeEmpty();
    }

    private static RoomView Room(int width, int length, (int X, int Y) at)
    {
        var room = new RoomView(1, SELF_PLAYER);
        room.SetMap(new HeightMap(width, length, new short[width * length]));
        room.AddAvatars([Avatar(SELF_PLAYER, SELF, at.X, at.Y)]);

        return room;
    }

    private static RoomAvatar Avatar(int playerId, int objectId, int x, int y) =>
        new(playerId, $"p{playerId}", objectId, x, y, 0, 2, RoomObjectType.Player);

    private static FloorItem Item(int objectId, int spriteId, int x, int y) =>
        new(objectId, spriteId, x, y, 0, 0, "0", SELF_PLAYER);

    private static async Task<HotelKnowledge> KnowledgeAsync()
    {
        var db = new InMemoryDb();

        await using (var context = db.CreateDbContext())
        {
            await context.FurnitureDefinitions.AddRangeAsync(
                [
                    Definition(SOLID, "table", walk: false, sit: false),
                    Definition(SEAT, "chair", walk: false, sit: true),
                    Definition(RUG, "rug", walk: true, sit: false),
                ],
                Ct
            );
            await context.SaveChangesAsync(Ct);
        }

        return await HotelKnowledge.LoadAsync(db, Ct);
    }

    private static FurnitureDefinitionEntity Definition(
        int spriteId,
        string name,
        bool walk,
        bool sit
    ) =>
        new()
        {
            SpriteId = spriteId,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = "default_floor",
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = false,
            CanWalk = walk,
            CanSit = sit,
            CanLay = false,
            CanRecycle = false,
            CanTrade = true,
            CanGroup = false,
            CanSell = false,
        };
}
