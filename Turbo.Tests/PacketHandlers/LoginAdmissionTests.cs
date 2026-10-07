using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Handshake;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Settings;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// Who the login lets in: a banned player is told why and turned away before the session knows
/// who they are, and in maintenance only those holding the bypass node get past.
/// </summary>
public class LoginAdmissionTests
{
    private const int PLAYER = 5;

    private static readonly DateTime NOW = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Fakes _fakes = new();
    private readonly Dictionary<string, string> _hotelTexts = [];
    private readonly ISessionContext _session;
    private readonly SSOTicketMessageHandler _handler;
    private PlayerSanctionSnapshot? _ban;
    private bool _admitted = true;

    public LoginAdmissionTests()
    {
        _fakes.Handlers["GetPlayerIdFromTicketAsync"] = _ => Task.FromResult(PLAYER);
        _fakes.Handlers["GetOwnedAsync"] = _ =>
            Task.FromResult(System.Collections.Immutable.ImmutableHashSet<int>.Empty);
        _fakes.Handlers["GetActiveBanAsync"] = _ => Task.FromResult(_ban);
        _fakes.Handlers["AdmitsAsync"] = _ => Task.FromResult(_admitted);
        HotelTextFakes.Use(_fakes, key => _hotelTexts.GetValueOrDefault(key));

        _session = _fakes.Create<ISessionContext>("session");
        _handler = new SSOTicketMessageHandler(
            _fakes.Create<IAuthenticationService>(),
            _fakes.Create<ISessionGateway>(),
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<INavigatorService>(),
            _fakes.Create<ISanctionService>(),
            _fakes.Create<IHotelAvailability>(),
            _fakes.Create<IHotelTextProvider>(),
            _fakes.Create<Turbo.Primitives.Figures.IPlayerClothingService>()
        );
    }

    private static PlayerSanctionSnapshot Ban(DateTime? until, string reason = "spam") =>
        new()
        {
            Id = 1,
            PlayerId = PLAYER,
            Kind = SanctionKind.Ban,
            Reason = reason,
            IssuedAtUtc = NOW,
            ExpiresAtUtc = until,
        };

    private Task LogInAsync() =>
        _handler
            .HandleAsync(
                new SSOTicketMessage { SSO = "ticket" },
                new MessageContext(_session, 0, -1),
                CancellationToken.None
            )
            .AsTask();

    private IEnumerable<object?> SentToSession() =>
        _fakes
            .Log.On<ISessionContext>()
            .Where(x => x.Method == "SendComposerAsync")
            .Select(x => x.Args[0]);

    private bool Closed =>
        _fakes.Log.On<ISessionContext>().Any(x => x.Method == "CloseSessionAsync");

    private bool Bound => _fakes.Log.Of("AddSessionToPlayerAsync").Any();

    [Fact]
    public async Task ABannedPlayer_IsToldWhy_AndTurnedAwayBeforeTheSessionKnowsWhoTheyAre()
    {
        _ban = Ban(NOW.AddDays(3), "spamming the lobby");

        await LogInAsync();

        SentToSession()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<UserBannedMessageComposer>()
            .Which.Message.Should()
            .Be(
                "You are banned from the hotel until 2026-10-04 12:00 UTC. Reason: spamming the lobby"
            );
        Closed.Should().BeTrue();
        Bound.Should().BeFalse();
    }

    [Fact]
    public async Task APermanentBan_SaysItHasNoEnd()
    {
        _ban = Ban(null);

        await LogInAsync();

        SentToSession()
            .OfType<UserBannedMessageComposer>()
            .Single()
            .Message.Should()
            .Be("You are banned from the hotel. Reason: spam");
    }

    [Fact]
    public async Task TheBanMessage_IsTheHotelsOwnWords_WhenItHasThem()
    {
        _hotelTexts["moderation.ban.message"] = "Banned until %1% (%0%).";
        _ban = Ban(NOW.AddHours(1));

        await LogInAsync();

        SentToSession()
            .OfType<UserBannedMessageComposer>()
            .Single()
            .Message.Should()
            .Be("Banned until 2026-10-01 13:00 UTC (spam).");
    }

    [Fact]
    public async Task InMaintenance_APlayerWhoMayNotStay_IsToldAndTurnedAway()
    {
        _admitted = false;

        await LogInAsync();

        SentToSession()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<HabboBroadcastMessageComposer>()
            .Which.Message.Should()
            .Be("The hotel is now in maintenance. Please come back later.");
        Closed.Should().BeTrue();
        Bound.Should().BeFalse();
    }

    [Fact]
    public async Task InMaintenance_TheMessageIsTheHotelsOwn_WhenItHasOne()
    {
        _hotelTexts[AvailabilityMessages.MAINTENANCE_STARTED] = "Back at six.";
        _admitted = false;

        await LogInAsync();

        SentToSession()
            .OfType<HabboBroadcastMessageComposer>()
            .Single()
            .Message.Should()
            .Be("Back at six.");
    }

    [Fact]
    public async Task APlayerWhoIsNeitherBannedNorTurnedAway_ReachesTheSession()
    {
        // Everything after the session is bound is the login proper, which the fakes here do not
        // answer: what matters is that it was reached, and nobody was sent away.
        await Record.ExceptionAsync(LogInAsync);

        Bound.Should().BeTrue();
        Closed.Should().BeFalse();
        SentToSession().OfType<UserBannedMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task SuccessfulLoginDeliversPendingRewardsAfterEnablingNotifications()
    {
        _fakes.Handlers["GetSettingsAsync"] = _ =>
            Task.FromResult(Activator.CreateInstance<PlayerSettingsSnapshot>());
        _fakes.Handlers["GetFavouriteRoomIdsAsync"] = _ =>
            Task.FromResult(ImmutableArray<RoomId>.Empty);
        _fakes.Handlers["GetClubGiftInfoAsync"] = _ => Task.FromResult(ClubGiftInfoSnapshot.Empty);
        _fakes.Handlers["GetUnseenItemsAsync"] = _ =>
            Task.FromResult(ImmutableDictionary<UnseenItemCategory, ImmutableArray<int>>.Empty);
        var summary = Activator.CreateInstance<PlayerSummarySnapshot>();
        summary = summary with { AchievementScore = 120 };
        _fakes.Handlers["GetSummaryAsync"] = _ => Task.FromResult(summary);

        await LogInAsync();

        var calls = _fakes.Log.Calls.ToList();
        var enabled = calls.FindIndex(x =>
            x.Method == "SendComposerAsync" && x.Args[0] is InfoFeedEnableMessageComposer
        );
        var delivered = calls.FindIndex(x => x.Method == "DeliverPendingRewardsAsync");
        enabled.Should().BeGreaterThan(-1);
        delivered.Should().BeGreaterThan(enabled);
        _fakes.Log.Of("DeliverPendingRewardsAsync").Should().ContainSingle();
        SentToSession()
            .OfType<AchievementsScoreEventMessageComposer>()
            .Single()
            .Score.Should()
            .Be(120);
        _fakes.Log.Of("ReconcileAsync").Should().ContainSingle();
    }
}
