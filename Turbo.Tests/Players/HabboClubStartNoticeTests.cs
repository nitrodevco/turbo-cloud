using FluentAssertions;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Users;
using Turbo.Players;
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
            GrainHarness.Create(
                typeof(PlayerModule).Assembly,
                "Turbo.Players.Grains.Subscriptions.PlayerSubscriptionGrain",
                _fakes,
                _db
            );

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
            .Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<ScrSendUserInfoMessageComposer>()
            .Single()
            .ResponseType.Should()
            .Be(ScrUserInfoResponseType.SubscriptionChanged);
        _fakes
            .Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<ClubGiftNotificationEventMessageComposer>()
            .Single()
            .NumGifts.Should()
            .Be(1);
        _fakes
            .Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .Should()
            .NotContain(x => x is BuildersClubSubscriptionStatusMessageComposer);
    }

    [Fact]
    public async Task The_club_info_asked_for_is_followed_by_the_gift_notice_when_gifts_wait()
    {
        await Handler().HandleAsync(new ScrGetUserInfoMessage { ProductName = "habbo_club" }, Context(), Ct);

        _fakes.Log.Of("SendClubInfoAsync").Should().ContainSingle();
        _fakes
            .Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<ClubGiftNotificationEventMessageComposer>()
            .Single()
            .NumGifts.Should()
            .Be(1);
    }

    [Fact]
    public async Task The_club_info_asked_for_brings_no_gift_notice_without_gifts()
    {
        _fakes.Handlers["GetClubGiftInfoAsync"] = _ => Task.FromResult(ClubGiftInfoSnapshot.Empty);

        await Handler().HandleAsync(new ScrGetUserInfoMessage { ProductName = "habbo_club" }, Context(), Ct);

        _fakes
            .Log.Of("SendComposerAsync")
            .Should()
            .NotContain(x => x.Args[0] is ClubGiftNotificationEventMessageComposer);
    }

    private ScrGetUserInfoMessageHandler Handler() => new(_fakes.Create<IGrainFactory>());

    private MessageContext Context() => new(_fakes.Create<ISessionContext>(), 1, -1);
}
