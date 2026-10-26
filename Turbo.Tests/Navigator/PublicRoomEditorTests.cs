using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// The Public tab as staff edit it: a room is in a category once and may be in several (Habbo
/// listed Sunset Ranch in New and Games, official client capture 2026-10-18), its place is the
/// order staff set (newest first unless put last), every change is published under the
/// <c>public</c> listing key so the next search on any silo reads it, and the staff picks
/// category takes no rooms by hand. A room deleted leaves every category.
/// </summary>
public sealed class PublicRoomEditorTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly PublicRoomEditor _editor;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PublicRoomEditorTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "owner",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );

        foreach (var id in new[] { 11, 12, 13 })
            _db.Insert(Room(id));

        _editor = new PublicRoomEditor(
            _db,
            _fakes.Create<IGrainFactory>(),
            NullLogger<IPublicRoomEditor>.Instance
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Rooms_keep_the_order_staff_set_once_per_category_and_every_change_is_published()
    {
        var newest = await Save("New");
        var games = await Save("Games");

        (await _editor.AddRoomAsync(newest, 11, first: true, by: 1, Ct)).Should().BeTrue();
        (await _editor.AddRoomAsync(newest, 12, first: true, by: 1, Ct)).Should().BeTrue();
        (await _editor.AddRoomAsync(newest, 13, first: false, by: null, Ct)).Should().BeTrue();
        (await _editor.AddRoomAsync(newest, 11, first: true, by: 1, Ct)).Should().BeFalse();
        (await _editor.AddRoomAsync(games, 11, first: true, by: 1, Ct)).Should().BeTrue();

        (await Rooms()).Should().Equal((newest, "12,11,13"), (games, "11"));

        await _editor.OrderRoomsAsync(newest, [13, 12, 11], Ct);
        await _editor.MoveRoomAsync(newest, 12, games, 1, Ct);

        (await Rooms()).Should().Equal((newest, "13,11"), (games, "11,12"));

        var reorderPart = () => _editor.OrderRoomsAsync(newest, [13], Ct);
        var moveOntoItself = () => _editor.MoveRoomAsync(newest, 11, games, 0, Ct);

        await reorderPart.Should().ThrowAsync<ArgumentException>();
        await moveOntoItself.Should().ThrowAsync<ArgumentException>();

        await _editor.OrderCategoriesAsync([games, newest], Ct);
        (await _editor.RemoveRoomAsync(games, 11, Ct)).Should().BeTrue();

        (await Rooms()).Should().Equal((games, "12"), (newest, "13,11"));
        _fakes
            .Log.Of("PublishListingChangesAsync")
            .Select(x => string.Join(',', (IReadOnlyCollection<string>)x.Args[0]!))
            .Should()
            .HaveCount(10)
            .And.OnlyContain(x => x == NavigatorListingKeys.PUBLIC);
    }

    [Fact]
    public async Task The_staff_picks_category_is_one_untitled_category_rooms_are_not_put_in_by_hand()
    {
        var picks = await _editor.SaveCategoryAsync(
            0,
            new PublicCategoryChange(Visible: false),
            NavigatorPublicCategoryKind.StaffPicks,
            null,
            Ct
        );
        var second = () =>
            _editor.SaveCategoryAsync(
                0,
                new PublicCategoryChange(),
                NavigatorPublicCategoryKind.StaffPicks,
                null,
                Ct
            );
        var byHand = () => _editor.AddRoomAsync(picks, 11, true, 1, Ct);
        var untitled = () =>
            _editor.SaveCategoryAsync(
                0,
                new PublicCategoryChange(Name: " "),
                NavigatorPublicCategoryKind.Manual,
                null,
                Ct
            );

        await second.Should().ThrowAsync<ArgumentException>();
        await byHand.Should().ThrowAsync<ArgumentException>();
        await untitled.Should().ThrowAsync<ArgumentException>();

        await using (var db = await _db.CreateDbContextAsync(Ct))
            await db
                .Rooms.Where(x => x.Id == 12)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StaffPick, true), Ct);

        (await _editor.ListAsync(Ct))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<PublicCategoryEntry>(x =>
                x.SearchCode == "staffpicks"
                && x.Name == ""
                && !x.Visible
                && x.Rooms.Single().RoomId == new RoomId(12)
            );
    }

    [Fact]
    public async Task A_deleted_room_leaves_every_category()
    {
        var newest = await Save("New");
        var games = await Save("Games");

        await _editor.AddRoomAsync(newest, 11, true, 1, Ct);
        await _editor.AddRoomAsync(games, 11, true, 1, Ct);

        await using (var db = await _db.CreateDbContextAsync(Ct))
            await db.Rooms.Where(x => x.Id == 11).ExecuteDeleteAsync(Ct);

        (await Rooms()).Should().Equal((newest, ""), (games, ""));
    }

    private Task<int> Save(string name) =>
        _editor.SaveCategoryAsync(
            0,
            new PublicCategoryChange(name),
            NavigatorPublicCategoryKind.Manual,
            null,
            Ct
        );

    private async Task<List<(int, string)>> Rooms() =>
        [
            .. (await _editor.ListAsync(Ct)).Select(x =>
                (x.Id, string.Join(',', x.Rooms.Select(r => r.RoomId.Value)))
            ),
        ];

    private static RoomEntity Room(int id) =>
        new()
        {
            Id = id,
            Name = $"room {id}",
            PlayerEntityId = 1,
            RoomModelEntityId = 1,
            DoorMode = RoomDoorModeType.Open,
            UsersNow = 0,
            PlayersMax = 25,
            WallHeight = -1,
            HideWalls = false,
            AllowBlocking = false,
            AllowPets = true,
            AllowPetsEat = true,
            ThicknessWall = RoomThicknessType.Normal,
            ThicknessFloor = RoomThicknessType.Normal,
            TradeType = RoomTradeModeType.Disabled,
            MuteType = ModSettingType.Owner,
            KickType = ModSettingType.Owner,
            BanType = ModSettingType.Owner,
            ChatFloodType = ChatFloodSensitivityType.Minimal,
            PlayerEntity = null!,
            RoomModelEntity = null!,
        };
}
