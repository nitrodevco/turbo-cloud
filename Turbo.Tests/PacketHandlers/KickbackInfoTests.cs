using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Events.Registry;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Users;
using Turbo.Players.Configuration;
using Turbo.Players.Grains.Subscriptions;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Events;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Subscriptions;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The club centre asks for ScrGetKickbackInfo when it opens. Habbo answers with the player's
/// streak and join date, a 0.1 share, the catalogue credits spent towards the coming payday and
/// the whole days left until it, in minutes (official client captures 2026-10-13 to 2026-10-26).
/// Turbo sent the same 2880 minutes and zero credits to everyone, whatever they had spent.
/// </summary>
public sealed class KickbackInfoTests : IDisposable
{
    private const int PLAYER = 1;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _clock = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public KickbackInfoTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "player",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
    }

    public void Dispose() => _db.Dispose();

    // Official client captures 2026-10-26T02-21-51 and 02-37-31: a non-member, 2 of 2 identical.
    // After the 10-16 payday it had spent 2 credits on a room linker, 50 + 50 on two chests and 25 + 25 on
    // two bots, and 2 + 1 on two credit furni coins (official client captures 2026-10-17 to
    // 2026-10-24); the reply counts 152 of those as missed.
    [Fact]
    [Trait("evidence", "inferred")]
    public async Task A_non_member_who_spent_is_answered_as_habbo_answered_one()
    {
        _clock.Set(new DateTime(2026, 10, 17, 16, 26, 0, DateTimeKind.Utc));
        await BuyAsync(2, "CF_1_coin_bronze");
        await BuyAsync(1, "CF_1_coin_bronze_d");
        _clock.Set(new DateTime(2026, 10, 18, 2, 0, 52, DateTimeKind.Utc));
        await BuyAsync(2, "wf_room_linker");
        _clock.Set(new DateTime(2026, 10, 24, 14, 4, 28, DateTimeKind.Utc));
        foreach (var (credits, name) in Purchases)
            await BuyAsync(credits, name);
        _clock.Set(new DateTime(2026, 10, 26, 2, 21, 51, DateTimeKind.Utc));

        var packet = PacketHarness.Encode(await AskAsync());

        // The official reply, byte for byte.
        Assert.Equal(
            Convert.FromHexString(
                "0000000000003FB999999999999A000000980000000000000000000000000000000000007080"
            ),
            packet.PopBytes(packet.Remaining)
        );
    }

    private static readonly (int Credits, string Name)[] Purchases =
    [
        (50, "wf_storage_coins2"),
        (50, "wf_storage_furni1"),
        (25, "bot_generic"),
        (25, "bot_generic"),
    ];

    // Every recorded countdown: whole days to a 15th, in minutes.
    [Theory]
    [InlineData("2026-10-13T17:48:15", 2880)]
    [InlineData("2026-10-16T03:33:12", 43200)]
    [InlineData("2026-10-17T03:14:59", 41760)]
    [InlineData("2026-10-26T02:21:51", 28800)]
    [InlineData("2026-10-26T02:37:31", 28800)]
    public async Task The_countdown_is_the_whole_days_left_to_the_15th(string at, int minutes)
    {
        _clock.Set(DateTime.SpecifyKind(DateTime.Parse(at), DateTimeKind.Utc));

        (await AskAsync()).TimeUntilPayday.Should().Be(minutes);
    }

    // Official client capture 2026-10-26, a member (UI, 1/1): HC with 19 days left, first joined
    // 31-01-2013, a 13 day streak, nothing spent: "You'll be getting: 5 credits", breakdown
    // "0 credits spent / + 10% / + 5 credits for streak bonus".
    [Fact]
    public async Task A_member_on_a_streak_who_spent_nothing_gets_the_streak_bonus()
    {
        var now = DateTime.UtcNow;

        _clock.Set(now);
        InsertClub(new DateTime(2013, 1, 31), now.AddDays(-13), now.AddDays(19));

        var reply = await AskAsync();

        reply.FirstSubscriptionDate.Should().Be("31-01-2013");
        reply.CurrentHcStreak.Should().Be(13);
        (reply.TotalCreditsSpent, reply.CreditRewardForMonthlySpent).Should().Be((0, 0));
        reply.CreditRewardForStreakBonus.Should().Be(5);
    }

    [Fact]
    public async Task The_streak_is_the_run_going_on_now_not_every_run()
    {
        var now = DateTime.UtcNow;

        _clock.Set(now);
        InsertClub(new DateTime(2013, 1, 31), now, now.AddDays(31));

        var reply = await AskAsync();

        // Habbo reads 0 and no bonus a minute after a returning member's box starts (2026-10-13).
        (reply.CurrentHcStreak, reply.CreditRewardForStreakBonus)
            .Should()
            .Be((0, 0));
    }

    // The breakdown shows the share as "+ 10% as HC Payday Credits" of what was spent
    // (ClubSpecialInfoBubbleView.as:40-53, WIN63-202610201032); a member's spend counts as spent.
    [Fact]
    public async Task A_members_spend_counts_towards_the_coming_payday_only()
    {
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

        _clock.Set(now);
        InsertClub(new DateTime(2013, 1, 31), DateTime.UtcNow, DateTime.UtcNow.AddDays(60));
        await BuyAsync(120, "bot_generic");
        await BuyAsync(5, "CF_5_coin_silver");

        var reply = await AskAsync();

        (reply.TotalCreditsSpent, reply.TotalCreditsMissed, reply.CreditRewardForMonthlySpent)
            .Should()
            .Be((120, 0, 12));

        _clock.Set(new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc));

        (await AskAsync()).TotalCreditsSpent.Should().Be(0, "the 11-15 payday has gone by");
    }

    /// <summary>The player's subscription grain, activated over what the database holds now.</summary>
    private async Task<IGrainFactory> GrainsAsync()
    {
        var key = (typeof(IPlayerSubscriptionGrain), (object?)(long)PLAYER);

        if (!_fakes.Instances.ContainsKey(key))
        {
            var grain = GrainHarness.Create<PlayerSubscriptionGrain>(_fakes, _db, PLAYER);

            GrainHarness.Replace<TimeProvider>(grain, _clock);
            await grain.OnActivateAsync(Ct);
            _fakes.Instances[key] = grain;
        }

        return _fakes.Create<IGrainFactory>();
    }

    private void InsertClub(DateTime firstJoined, DateTime runStarted, DateTime expires) =>
        _db.Insert(
            new PlayerSubscriptionEntity
            {
                PlayerEntityId = PLAYER,
                SubscriptionType = SubscriptionType.HabboClub,
                FirstSubscribedAt = firstJoined,
                RunStartedAt = runStarted,
                ExpiresAt = expires,
                TotalDaysSubscribed = 600,
            }
        );

    private async Task BuyAsync(int credits, string productCode) =>
        await new HcPaydaySpendHandler(await GrainsAsync())
            .HandleAsync(
                new PurchaseCompletedEvent
                {
                    PlayerId = PlayerId.Parse(PLAYER),
                    Kind = PurchaseKind.Catalog,
                    CatalogType = CatalogType.Normal,
                    OfferId = 1,
                    ProductCode = productCode,
                    Units = 1,
                    CostCredits = credits,
                    GivesCreditFurni = productCode.StartsWith("CF_", StringComparison.Ordinal),
                },
                new EventContext(),
                Ct
            )
            .AsTask();

    private async Task<ScrSendKickbackInfoMessageComposer> AskAsync()
    {
        await new ScrGetKickbackInfoMessageHandler(
            await GrainsAsync(),
            Options.Create(new PlayerConfig())
        ).HandleAsync(
            new ScrGetKickbackInfoMessage(),
            new MessageContext(_fakes.Create<ISessionContext>(), PLAYER, 7),
            Ct
        );

        return _fakes.Sent<ScrSendKickbackInfoMessageComposer>().Last();
    }
}
