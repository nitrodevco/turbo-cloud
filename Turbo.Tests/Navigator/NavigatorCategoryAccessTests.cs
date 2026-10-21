using System.Collections.Immutable;
using FluentAssertions;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Navigator;
using Turbo.PacketHandlers.NewNavigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.NewNavigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Navigator;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Players.Snapshots.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

public class NavigatorCategoryAccessTests
{
    [Fact]
    public void RetroDefaultMinRank_IsEveryones()
    {
        NavigatorCategoryAccess
            .CanSee(Category(minRank: 1), Holds(), SecurityLevelType.None)
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData(SecurityLevelType.None, false)]
    [InlineData(SecurityLevelType.Employee, false)]
    [InlineData(SecurityLevelType.Moderator, true)]
    [InlineData(SecurityLevelType.Administrator, true)]
    public void MinRank_ComparesWithSecurityLevel(SecurityLevelType level, bool expected)
    {
        NavigatorCategoryAccess.CanSee(Category(minRank: 5), Holds(), level).Should().Be(expected);
    }

    [Fact]
    public void StaffOnly_NeedsTheStaffCategoryNode_NotJustALevel()
    {
        var category = Category(staffOnly: true);

        NavigatorCategoryAccess
            .CanSee(category, Holds(), SecurityLevelType.Administrator)
            .Should()
            .BeFalse();
        NavigatorCategoryAccess
            .CanSee(
                category,
                Holds(PermissionNodes.Navigator.CATEGORY_STAFF),
                SecurityLevelType.Community
            )
            .Should()
            .BeTrue();
    }

    [Fact]
    public void RequiredNode_MustBeHeld()
    {
        var category = Category(requiredNode: "casino.vip");

        NavigatorCategoryAccess
            .CanSee(category, Holds(), SecurityLevelType.None)
            .Should()
            .BeFalse();
        NavigatorCategoryAccess
            .CanSee(category, Holds("casino.vip"), SecurityLevelType.None)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Invisible_IsNobodys()
    {
        NavigatorCategoryAccess
            .CanSee(
                Category(visible: false),
                Holds(PermissionNodes.Navigator.CATEGORY_STAFF),
                SecurityLevelType.Administrator
            )
            .Should()
            .BeFalse();
    }

    [Fact]
    public void Invisible_IsStillKnown_SoTheClientCanNameItsRooms()
    {
        // Habbo's UserFlatCats lists its hidden categories too (global official, TRADING, ...)
        // with visible false; the client keeps them out of its pickers only.
        var hidden = Category(visible: false);

        NavigatorCategoryAccess.CanKnow(hidden, Holds(), SecurityLevelType.None).Should().BeTrue();
        NavigatorCategoryAccess
            .CanKnow(Category(visible: false, staffOnly: true), Holds(), SecurityLevelType.None)
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task UserCategories_TellTheClientAboutHiddenCategoriesToo()
    {
        var fakes = new Fakes();
        fakes.Handlers["GetKnownFlatCategoriesForPlayerAsync"] = _ =>
            Task.FromResult<ImmutableArray<NavigatorFlatCategorySnapshot>>([
                Category(),
                Category(visible: false),
            ]);
        fakes.Handlers["GetEventCategories"] = _ =>
            (ImmutableArray<NavigatorEventCategorySnapshot>)
                [
                    new()
                    {
                        Id = 1,
                        Name = "Hottest Events",
                        Visible = false,
                    },
                    new()
                    {
                        Id = 2,
                        Name = "Parties & Music",
                        Visible = true,
                    },
                ];
        var service = fakes.Create<INavigatorService>();
        var ctx = new MessageContext(fakes.Create<ISessionContext>(), 1, -1);
        var ct = TestContext.Current.CancellationToken;

        await new GetUserFlatCatsMessageHandler(service).HandleAsync(new(), ctx, ct);
        await new GetUserEventCatsMessageHandler(service).HandleAsync(new(), ctx, ct);

        var sent = fakes.Sent().ToList();
        sent.OfType<UserFlatCatsMessageComposer>().Single().Categories.Should().HaveCount(2);
        sent.OfType<UserEventCatsMessageComposer>()
            .Single()
            .EventCategories.Select(x => x.Name)
            .Should()
            .Equal("Hottest Events", "Parties & Music");
    }

    [Fact]
    public async Task NavigatorInit_StartsWithTheUserAndEventCategories()
    {
        // hhus, two recordings (2026-10-13): NewNavigatorInit is answered with UserFlatCats,
        // UserEventCats, NavigatorMetaData, NavigatorLiftedRooms, NavigatorCollapsedCategories,
        // NavigatorSavedSearches and NewNavigatorPreferences, in that order.
        var fakes = new Fakes();
        fakes.Handlers["GetKnownFlatCategoriesForPlayerAsync"] = _ =>
            Task.FromResult<ImmutableArray<NavigatorFlatCategorySnapshot>>([Category()]);
        fakes.Handlers["GetEventCategories"] = _ =>
            (ImmutableArray<NavigatorEventCategorySnapshot>)
                [
                    new()
                    {
                        Id = 2,
                        Name = "Parties & Music",
                        Visible = true,
                    },
                ];
        fakes.Handlers["GetTopLevelContextAsync"] = _ =>
            Task.FromResult(ImmutableArray<NavigatorTopLevelContextSnapshot>.Empty);
        fakes.Handlers["GetSnapshotAsync"] = _ =>
            Task.FromResult(
                new PlayerNavigatorSnapshot
                {
                    FavouriteRoomIds = [],
                    SavedSearches = [],
                    CollapsedSearchCodes = [],
                    ViewModes = ImmutableDictionary<string, NavigatorViewModeType>.Empty,
                }
            );
        fakes.Handlers["GetSettingsAsync"] = _ => Task.FromResult(Settings());
        var ctx = new MessageContext(fakes.Create<ISessionContext>(), 1, -1);

        await new NewNavigatorInitMessageHandler(
            fakes.Create<INavigatorService>(),
            fakes.Create<IGrainFactory>()
        ).HandleAsync(new(), ctx, TestContext.Current.CancellationToken);

        fakes
            .Sent()
            .Select(x => x.GetType().Name)
            .Should()
            .Equal(
                nameof(UserFlatCatsMessageComposer),
                nameof(UserEventCatsMessageComposer),
                nameof(NavigatorMetaDataMessage),
                nameof(NavigatorLiftedRoomsMessage),
                nameof(NavigatorCollapsedCategoriesMessage),
                nameof(NavigatorSavedSearchesMessage),
                nameof(NewNavigatorPreferencesMessageComposer)
            );
    }

    private static PlayerSettingsSnapshot Settings() =>
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
        };

    private static NavigatorFlatCategorySnapshot Category(
        int minRank = 1,
        bool staffOnly = false,
        bool visible = true,
        string? requiredNode = null
    ) =>
        new()
        {
            Id = 1,
            Name = "test",
            Visible = visible,
            Automatic = false,
            AutomaticCategoryKey = string.Empty,
            GlobalCategoryKey = string.Empty,
            StaffOnly = staffOnly,
            MinRank = minRank,
            OrderNum = 0,
            RequiredNode = requiredNode,
        };

    private static ResolvedPermissionsSnapshot Holds(params string[] nodes) =>
        ResolvedPermissionsSnapshot.EMPTY with
        {
            Granted = [.. nodes],
        };
}
