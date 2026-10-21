using FluentAssertions;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Users;
using Turbo.Players.Grains.Subscriptions;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Subscriptions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// What a player is told when their Habbo Club starts, and what follows the club info they ask
/// for. Official client capture of the 31 days HC box (2026-10-13): <c>UserRights</c> club level 2,
/// <c>ScrSendUserInfo</c> response type 2 and <c>ClubGiftNotification</c> 1 in the same
/// millisecond, nothing else between; every <c>ScrSendUserInfo</c> answer to a member is followed
/// by <c>ClubGiftNotification</c> too (15 of 15 recorded).
/// </summary>
public sealed class HabboClubStartNoticeTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public HabboClubStartNoticeTests()
    {
        _fakes.Handlers["GetClubGiftInfoAsync"] = _ =>
            Task.FromResult(ClubGiftInfoSnapshot.Empty with { GiftsAvailable = 1 });
        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "member",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_club_that_starts_sends_the_club_level_the_club_info_and_the_gift_notice_in_that_order()
    {
        var grain = (IPlayerSubscriptionGrain)
            GrainHarness.Create<PlayerSubscriptionGrain>(_fakes, _db);

        await grain.ExtendAsync(SubscriptionType.HabboClub, 31, Ct);

        var told = _fakes
            .Log.Calls.Where(x =>
                x.Method == "SendClubLevelAsync"
                || x.Method == "SendClientStateAsync"
                || x.Method == "SendComposerAsync"
            )
            .Select(x => x.Method == "SendComposerAsync" ? x.Args[0]!.GetType().Name : x.Method)
            .ToList();

        told.Should()
            .Equal(
                "SendClubLevelAsync",
                nameof(ScrSendUserInfoMessageComposer),
                nameof(ClubGiftNotificationEventMessageComposer)
            );
        _fakes
            .Sent<ScrSendUserInfoMessageComposer>()
            .Single()
            .ResponseType.Should()
            .Be(ScrUserInfoResponseType.SubscriptionChanged);
        _fakes.Sent<ClubGiftNotificationEventMessageComposer>().Single().NumGifts.Should().Be(1);
        _fakes.Sent().Should().NotContain(x => x is BuildersClubSubscriptionStatusMessageComposer);
    }

    /// <summary>
    /// The same capture: a fresh 31 days reads <c>daysToPeriodEnd</c> 31,
    /// <c>periodsSubscribedAhead</c> 0, <c>minutesUntilExpiration</c> 44639. The client adds the
    /// two back up as periods * 31 + days (ClubBuyCatalogWidget.as:96, PurseClubArea.as:92), so
    /// every length splits the same way: the period being served is never one ahead.
    /// </summary>
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(3, 3, 0)]
    [InlineData(7, 7, 0)]
    [InlineData(30, 30, 0)]
    [InlineData(31, 31, 0)]
    [InlineData(32, 1, 1)]
    [InlineData(62, 31, 1)]
    [InlineData(93, 31, 2)]
    [InlineData(186, 31, 5)]
    [InlineData(365, 24, 11)]
    public async Task The_club_info_splits_the_days_left_into_the_period_being_served_and_whole_periods_ahead(
        int days,
        int daysToPeriodEnd,
        int periodsAhead
    )
    {
        await Grain().ExtendAsync(SubscriptionType.HabboClub, days, Ct);

        var info = SentClubInfo();

        info.DaysToPeriodEnd.Should().Be(daysToPeriodEnd);
        info.PeriodsSubscribedAhead.Should().Be(periodsAhead);
        (info.PeriodsSubscribedAhead * 31 + info.DaysToPeriodEnd).Should().Be(days);
        info.MinutesUntilExpiration.Should().BeInRange(days * 24 * 60 - 1, days * 24 * 60);
    }

    [Fact]
    public async Task Member_periods_count_the_periods_already_served_not_the_new_one()
    {
        _db.Insert(
            new PlayerSubscriptionEntity
            {
                PlayerEntityId = 1,
                SubscriptionType = SubscriptionType.HabboClub,
                FirstSubscribedAt = DateTime.UtcNow.AddYears(-10),
                ExpiresAt = DateTime.UtcNow.AddYears(-1),
                TotalDaysSubscribed = 566,
            }
        );

        await Grain().ExtendAsync(SubscriptionType.HabboClub, 31, Ct);

        var info = SentClubInfo();

        info.MemberPeriods.Should().Be(18);
        info.PastClubDays.Should().Be(566);
        info.DaysToPeriodEnd.Should().Be(31);
        info.PeriodsSubscribedAhead.Should().Be(0);
    }

    [Fact]
    public async Task A_membership_that_starts_again_starts_a_new_streak()
    {
        _db.Insert(
            new PlayerSubscriptionEntity
            {
                PlayerEntityId = 1,
                SubscriptionType = SubscriptionType.HabboClub,
                FirstSubscribedAt = DateTime.UtcNow.AddYears(-10),
                RunStartedAt = DateTime.UtcNow.AddYears(-3),
                ExpiresAt = DateTime.UtcNow.AddYears(-1),
                TotalDaysSubscribed = 566,
            }
        );
        var grain = Grain();

        await grain.ExtendAsync(SubscriptionType.HabboClub, 31, Ct);

        (await grain.GetAsync(SubscriptionType.HabboClub, Ct)).CurrentStreakDays.Should().Be(0);
    }

    [Fact]
    public async Task A_membership_extended_while_it_runs_keeps_its_streak()
    {
        _db.Insert(
            new PlayerSubscriptionEntity
            {
                PlayerEntityId = 1,
                SubscriptionType = SubscriptionType.HabboClub,
                FirstSubscribedAt = DateTime.UtcNow.AddDays(-40),
                RunStartedAt = DateTime.UtcNow.AddDays(-40).AddHours(-1),
                ExpiresAt = DateTime.UtcNow.AddDays(22),
                TotalDaysSubscribed = 62,
            }
        );
        var grain = Grain();

        await grain.ExtendAsync(SubscriptionType.HabboClub, 31, Ct);

        (await grain.GetAsync(SubscriptionType.HabboClub, Ct)).CurrentStreakDays.Should().Be(40);
    }

    private IPlayerSubscriptionGrain Grain() =>
        (IPlayerSubscriptionGrain)GrainHarness.Create<PlayerSubscriptionGrain>(_fakes, _db);

    private ScrSendUserInfoMessageComposer SentClubInfo() =>
        _fakes.Sent<ScrSendUserInfoMessageComposer>().Single();

    [Fact]
    public async Task The_club_info_asked_for_is_followed_by_the_gift_notice_when_gifts_wait()
    {
        await Handler()
            .HandleAsync(new ScrGetUserInfoMessage { ProductName = "habbo_club" }, Context(), Ct);

        _fakes.Log.Of("SendClubInfoAsync").Should().ContainSingle();
        _fakes.Sent<ClubGiftNotificationEventMessageComposer>().Single().NumGifts.Should().Be(1);
    }

    [Fact]
    public async Task The_club_info_asked_for_brings_no_gift_notice_without_gifts()
    {
        _fakes.Handlers["GetClubGiftInfoAsync"] = _ => Task.FromResult(ClubGiftInfoSnapshot.Empty);

        await Handler()
            .HandleAsync(new ScrGetUserInfoMessage { ProductName = "habbo_club" }, Context(), Ct);

        _fakes.Sent().Should().NotContain(x => x is ClubGiftNotificationEventMessageComposer);
    }

    private ScrGetUserInfoMessageHandler Handler() => new(_fakes.Create<IGrainFactory>());

    private MessageContext Context() => new(_fakes.Create<ISessionContext>(), 1, -1);
}
