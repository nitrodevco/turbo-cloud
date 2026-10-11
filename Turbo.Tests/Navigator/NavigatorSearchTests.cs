using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Navigator;
using Turbo.Navigator.Configuration;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// The navigator's room search finds what a player types in any case: a room by part of its name,
/// a room by its owner's name, and a room by one of its tags. PostgreSQL compares text exactly,
/// which a search must not.
/// </summary>
public sealed class NavigatorSearchTests : IDisposable
{
    private const int JOSH = 1;

    private readonly SqliteDb _db = new();
    private readonly NavigatorProvider _navigator;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public NavigatorSearchTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = JOSH,
                Name = "Josh",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
            }
        );
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model_a",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(Room(1, "Pool Party", ["fun", "pool"]));
        _db.Insert(Room(2, "Quiet Library", []));
        _navigator = new NavigatorProvider(
            _db,
            Options.Create(new NavigatorConfig()),
            NullLogger<NavigatorProvider>.Instance
        );
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(NavigatorSearchFilterType.RoomName, "pool party", "Pool Party")]
    [InlineData(NavigatorSearchFilterType.RoomName, "LIBRARY", "Quiet Library")]
    [InlineData(NavigatorSearchFilterType.Tag, "FUN", "Pool Party")]
    [InlineData(NavigatorSearchFilterType.Anything, "party", "Pool Party")]
    public async Task A_room_is_found_whatever_the_case_typed(
        NavigatorSearchFilterType filter,
        string typed,
        string room
    )
    {
        var found = await _navigator.SearchRoomsAsync(filter, typed, 10, Ct);

        found.Select(x => x.Name).Should().Equal(room);
    }

    [Fact]
    public async Task A_players_rooms_are_found_by_their_name_in_any_case()
    {
        var found = await _navigator.SearchRoomsAsync(
            NavigatorSearchFilterType.Owner,
            "JOSH",
            10,
            Ct
        );

        found.Select(x => x.Name).Should().BeEquivalentTo("Pool Party", "Quiet Library");
    }

    [Fact]
    public async Task A_percent_sign_is_looked_for_as_itself()
    {
        var found = await _navigator.SearchRoomsAsync(
            NavigatorSearchFilterType.RoomName,
            "%",
            10,
            Ct
        );

        found.Should().BeEmpty();
    }

    private static RoomEntity Room(int id, string name, List<string> tags) =>
        new()
        {
            Id = id,
            Name = name,
            PlayerEntityId = JOSH,
            RoomModelEntityId = 1,
            DoorMode = RoomDoorModeType.Open,
            PlayersMax = 25,
            WallHeight = -1,
            HideWalls = false,
            ThicknessWall = RoomThicknessType.Normal,
            ThicknessFloor = RoomThicknessType.Normal,
            AllowWalkThrough = false,
            AllowPets = true,
            AllowPetsEat = true,
            TradeType = RoomTradeModeType.Disabled,
            MuteType = ModSettingType.Owner,
            KickType = ModSettingType.Owner,
            BanType = ModSettingType.Owner,
            ChatFloodType = ChatFloodSensitivityType.Minimal,
            Tags = tags,
            PlayerEntity = null!,
            RoomModelEntity = null!,
        };
}
