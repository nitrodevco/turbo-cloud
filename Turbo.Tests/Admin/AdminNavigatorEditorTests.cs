using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Content;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Navigator;
using Turbo.Navigator.Configuration;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The navigator's categories edited in the panel: a change is what the running navigator lists
/// at once, a renamed or removed category's cached listings are dropped, and a category rooms are
/// still in can't be removed.
/// </summary>
public sealed class AdminNavigatorEditorTests : IDisposable
{
    private const int CATEGORY = 5;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly NavigatorProvider _navigator;
    private readonly AdminNavigatorEditor _editor;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminNavigatorEditorTests()
    {
        _db.Insert(
            new NavigatorFlatCategoryEntity
            {
                Id = CATEGORY,
                Name = "Chat",
                Visible = true,
                Automatic = false,
                StaffOnly = false,
                MinRank = 1,
                OrderNum = 0,
            }
        );
        _navigator = new NavigatorProvider(
            _db,
            Options.Create(new NavigatorConfig()),
            NullLogger<NavigatorProvider>.Instance
        );
        _editor = new AdminNavigatorEditor(
            _db,
            _navigator,
            _fakes.Create<IGrainFactory>(),
            NullLogger<AdminNavigatorEditor>.Instance
        );
    }

    public void Dispose()
    {
        _navigator.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task A_category_added_or_renamed_is_what_the_navigator_lists_at_once()
    {
        await _navigator.ReloadAsync(Ct);

        var added = await _editor.SaveFlatCategoryAsync(
            0,
            new NavigatorFlatCategoryRequest("Games", true, true, 1, null, 2, null, null, null),
            Ct
        );

        _navigator
            .GetFlatCategories()
            .Should()
            .ContainSingle(x => x.Id == added)
            .Which.StaffOnly.Should()
            .BeTrue();

        await _editor.SaveFlatCategoryAsync(
            CATEGORY,
            new NavigatorFlatCategoryRequest(
                "Hangouts",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ),
            Ct
        );

        _navigator
            .GetFlatCategories()
            .Select(x => x.Name)
            .Should()
            .Contain("Hangouts")
            .And.NotContain("Chat");
        _fakes
            .Log.Of("PublishListingChangesAsync")
            .Select(x => (IReadOnlyCollection<string>)x.Args[0]!)
            .Should()
            .ContainSingle(x => x.Contains(NavigatorListingKeys.Category(CATEGORY)));
    }

    [Fact]
    public async Task A_category_rooms_are_in_is_kept_until_they_move()
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
        _db.Insert(
            new RoomEntity
            {
                Id = 9,
                Name = "a room",
                PlayerEntityId = 1,
                RoomModelEntityId = 1,
                NavigatorCategoryEntityId = CATEGORY,
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
            }
        );

        (await _editor.GetAsync(Ct))
            .FlatCategories.Should()
            .ContainSingle()
            .Which.Rooms.Should()
            .Be(1);

        var remove = () => _editor.DeleteFlatCategoryAsync(CATEGORY, Ct);

        (await remove.Should().ThrowAsync<ArgumentException>()).WithMessage("1 rooms are in Chat*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Chat")]
    public async Task A_category_without_a_name_of_its_own_is_refused(string name)
    {
        var save = () =>
            _editor.SaveFlatCategoryAsync(
                0,
                new NavigatorFlatCategoryRequest(
                    name,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null
                ),
                Ct
            );

        await save.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Event_categories_and_tabs_are_added_hidden_and_removed()
    {
        await _navigator.ReloadAsync(Ct);

        var events = await _editor.SaveEventCategoryAsync(
            0,
            new NavigatorEventCategoryRequest("Parties", true),
            Ct
        );

        _navigator.GetEventCategories().Should().ContainSingle(x => x.Id == events);

        var tab = await _editor.SaveContextAsync(
            0,
            new NavigatorContextRequest("hotel_view", true, 0),
            Ct
        );

        (await _navigator.GetTopLevelContextsAsync())
            .Select(x => x.SearchCode)
            .Should()
            .Contain("hotel_view");

        await _editor.SaveContextAsync(
            tab,
            new NavigatorContextRequest("hotel_view", false, 0),
            Ct
        );

        (await _navigator.GetTopLevelContextsAsync())
            .Should()
            .BeEmpty("only visible tabs are loaded");

        (await _editor.DeleteEventCategoryAsync(events, Ct)).Should().BeTrue();
        _navigator.GetEventCategories().Should().BeEmpty();
    }
}
