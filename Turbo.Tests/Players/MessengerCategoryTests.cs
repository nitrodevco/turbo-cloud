using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Messenger;
using Turbo.Database.Entities.Players;
using Turbo.Players.Configuration;
using Turbo.Players.Grains.Messenger;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Grains.Messenger;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots.Settings;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms;
using Turbo.Rooms.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// Friend list categories and the "Friends can follow me" privacy setting. Flash lists the
/// categories <c>MessengerInit</c> / <c>FriendListUpdate</c> carry and has no message to manage
/// them: its edit button opens the hotel's web page (<c>FriendListView.as:188-197</c>,
/// WIN63-202610201032), so the server keeps them and the site or a plugin changes them, behind
/// <c>MessengerCategoriesEnabled</c> (off, as Habbo's <c>friendship.category.management.enabled</c>).
/// A friend who stops following has <c>FriendData.followingAllowed</c> false and a follow answered
/// with error 3 (<c>friendlist.followerror.prevented</c>).
/// </summary>
public sealed class MessengerCategoryTests : System.IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    public MessengerCategoryTests()
    {
        foreach (var id in new[] { 1, 2, 3 })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = $"friend{id}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        foreach (var id in new[] { 2, 3 })
            _db.Insert(
                new MessengerFriendEntity
                {
                    Id = id,
                    PlayerEntityId = 1,
                    FriendPlayerEntityId = id,
                    RelationType = MessengerFriendRelationType.Zero,
                    PlayerEntity = null!,
                    FriendPlayerEntity = null!,
                }
            );

        _fakes.Handlers["IsClean"] = _ => true;
        _fakes.Handlers["HasActiveSessionAsync"] = _ => Task.FromResult(true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Categories_cannot_be_managed_while_the_hotel_has_it_off()
    {
        var grain = Messenger(new PlayerConfig());

        var (result, category) = await grain.CreateCategoryAsync("Best", Ct);

        result.Should().Be(MessengerCategoryResultType.Disabled);
        category.Should().BeNull();
        (await grain.SetFriendCategoryAsync(2, 0, Ct))
            .Should()
            .Be(MessengerCategoryResultType.Disabled);
        _db.CreateDbContext().MessengerCategories.Should().BeEmpty();
    }

    [Fact]
    public async Task A_category_is_stored_and_a_friend_filed_under_it_until_it_is_removed()
    {
        var grain = Messenger(Enabled());

        var (result, category) = await grain.CreateCategoryAsync("  Best  ", Ct);

        result.Should().Be(MessengerCategoryResultType.Done);
        category!.Name.Should().Be("Best");
        (await grain.GetCategoriesAsync(Ct)).Should().ContainSingle(x => x.Name == "Best");
        // FriendCategories.onFriendListUpdate -> updateCategories: the client hears the list.
        _fakes
            .Sent<FriendListUpdateMessageComposer>()
            .Should()
            .Contain(x => x.Categories.Any(c => c.Name == "Best"));

        (await grain.SetFriendCategoryAsync(2, category.CategoryId, Ct))
            .Should()
            .Be(MessengerCategoryResultType.Done);
        Friend(grain, 2).CategoryId.Should().Be(category.CategoryId);
        FriendRow(2).MessengerCategoryEntityId.Should().Be(category.CategoryId);

        (await grain.RenameCategoryAsync(category.CategoryId, "Mates", Ct))
            .Should()
            .Be(MessengerCategoryResultType.Done);
        _db.CreateDbContext().MessengerCategories.Single().Name.Should().Be("Mates");

        (await grain.DeleteCategoryAsync(category.CategoryId, Ct))
            .Should()
            .Be(MessengerCategoryResultType.Done);
        (await grain.GetCategoriesAsync(Ct)).Should().BeEmpty();
        Friend(grain, 2).CategoryId.Should().Be(0);
        FriendRow(2).MessengerCategoryEntityId.Should().BeNull();
        _db.CreateDbContext().MessengerCategories.Should().BeEmpty();
    }

    [Fact]
    public async Task Names_and_the_number_of_categories_follow_the_settings()
    {
        var grain = Messenger(
            new PlayerConfig
            {
                MessengerCategoriesEnabled = true,
                MessengerMaxCategories = 2,
                MessengerCategoryNameMaxLength = 5,
            }
        );

        (await grain.CreateCategoryAsync("Best", Ct))
            .Result.Should()
            .Be(MessengerCategoryResultType.Done);
        (await grain.CreateCategoryAsync("BEST", Ct))
            .Result.Should()
            .Be(MessengerCategoryResultType.NameTaken);
        (await grain.CreateCategoryAsync("   ", Ct))
            .Result.Should()
            .Be(MessengerCategoryResultType.InvalidName);
        (await grain.CreateCategoryAsync("Toolong", Ct))
            .Result.Should()
            .Be(MessengerCategoryResultType.InvalidName);
        (await grain.CreateCategoryAsync("Work", Ct))
            .Result.Should()
            .Be(MessengerCategoryResultType.Done);
        (await grain.CreateCategoryAsync("More", Ct))
            .Result.Should()
            .Be(MessengerCategoryResultType.LimitReached);
        (await grain.SetFriendCategoryAsync(2, 999, Ct))
            .Should()
            .Be(MessengerCategoryResultType.CategoryNotFound);
        (await grain.SetFriendCategoryAsync(9, 0, Ct))
            .Should()
            .Be(MessengerCategoryResultType.NotFriend);
    }

    [Fact]
    public async Task A_friend_who_lets_no_one_follow_shows_no_follow_button()
    {
        _db.Insert(
            new PlayerSettingsEntity
            {
                Id = 1,
                PlayerEntityId = 2,
                FollowingDisabled = true,
            }
        );

        var friends = await Messenger(new PlayerConfig()).GetFriendsAsync(Ct);

        friends
            .Single(x => x.PlayerId == 2)
            .Should()
            .Match<MessengerFriendDto>(x => x.Online && !x.FollowingAllowed);
        friends
            .Single(x => x.PlayerId == 3)
            .Should()
            .Match<MessengerFriendDto>(x => x.Online && x.FollowingAllowed);
    }

    [Fact]
    public async Task A_friend_changing_the_setting_updates_the_follow_button_at_once()
    {
        var grain = Messenger(new PlayerConfig());

        await grain.GetFriendsAsync(Ct);
        await grain.ReceiveFriendFollowingAsync(2, false, Ct);

        Friend(grain, 2).FollowingAllowed.Should().BeFalse();

        await grain.ReceiveFriendFollowingAsync(2, true, Ct);

        Friend(grain, 2).FollowingAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Following_a_friend_who_lets_no_one_follow_is_prevented()
    {
        _fakes.Handlers["IsFriendAsync"] = _ => Task.FromResult(true);
        _fakes.Handlers["GetSettingsAsync"] = _ =>
            Task.FromResult(Settings(followingDisabled: true));

        var service = new RoomService(
            NullLogger<IRoomService>.Instance,
            Options.Create(new RoomConfig()),
            _fakes.Create<Orleans.IGrainFactory>(),
            System.TimeProvider.System
        );

        (await service.FollowFriendAsync(1, 2, Ct))
            .Should()
            .Be(FollowFriendErrorCodeType.Prevented);
        _fakes.Log.Of("GetActiveRoomAsync").Should().BeEmpty();
    }

    private static PlayerConfig Enabled() => new() { MessengerCategoriesEnabled = true };

    private IPlayerMessengerGrain Messenger(PlayerConfig config)
    {
        var grain = GrainHarness.Create<PlayerMessengerGrain>(_fakes, _db, playerId: 1);
        GrainHarness.Replace(grain, config);
        var friends = GrainHarness.Get<PlayerMessengerLiveState>(grain).Friends;
        friends[2] = new MessengerFriendDto { PlayerId = 2, Name = "friend2" };
        friends[3] = new MessengerFriendDto { PlayerId = 3, Name = "friend3" };

        return (IPlayerMessengerGrain)grain;
    }

    private static MessengerFriendDto Friend(IPlayerMessengerGrain grain, int id) =>
        GrainHarness.Get<PlayerMessengerLiveState>(grain).Friends[id];

    private MessengerFriendEntity FriendRow(int id) =>
        _db.CreateDbContext()
            .MessengerFriends.Single(x => x.PlayerEntityId == 1 && x.FriendPlayerEntityId == id);

    private static PlayerSettingsSnapshot Settings(bool followingDisabled) =>
        new()
        {
            GenericVolume = 100,
            FurniVolume = 100,
            TraxVolume = 100,
            RoomInvitesIgnored = false,
            RoomCameraFollowDisabled = false,
            UIFlags = default,
            ChatStyleId = 0,
            ChatFontSize = default,
            ChatMode = default,
            ChatBubbleWidth = default,
            ChatScrollSpeed = default,
            WiredMenuButton = false,
            WiredInspectButton = false,
            WiredPlayTestMode = false,
            WiredVariableSyntaxMode = 0,
            WiredWhisperDisabled = false,
            WiredShowAllNotifications = false,
            WiredUIStyle = string.Empty,
            OnlineIndicatorPreference = 0,
            HomeRoomId = -1,
            NavigatorWindowX = 0,
            NavigatorWindowY = 0,
            NavigatorWindowWidth = 0,
            NavigatorWindowHeight = 0,
            NavigatorLeftPaneHidden = false,
            NavigatorResultsMode = default,
            FriendRequestsDisabled = false,
            ProfileHidden = false,
            FollowingDisabled = followingDisabled,
        };
}
