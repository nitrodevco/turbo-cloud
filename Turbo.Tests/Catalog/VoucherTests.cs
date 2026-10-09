using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Vouchers;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Vouchers as a player redeems them in the catalogue (<c>RedeemVoucher</c>): its rewards given
/// once - credits and another currency under the voucher's reference, furniture, a badge - and
/// the client told as its alerts read it: the furniture's name and description, or an error code.
/// A player redeems a voucher once, a voucher is never used past its limit, and a player who keeps
/// getting codes wrong is stopped.
/// </summary>
public sealed class VoucherTests : IDisposable
{
    private const int ALICE = 7;
    private const int BOB = 8;

    private static readonly DateTime NOW = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly CatalogFixture _catalog = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(NOW));
    private readonly VoucherService _vouchers;
    private readonly List<(
        long Player,
        CurrencyKind Kind,
        int Amount,
        string Reference
    )> _credits = [];
    private readonly List<(long Player, int Definition)> _furniture = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public VoucherTests()
    {
        foreach (var (id, name) in new[] { (ALICE, "alice"), (BOB, "bob") })
            _catalog.Db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        var fakes = _catalog.Fakes;

        fakes.Handlers["CreditAsync"] = call =>
        {
            _credits.Add(
                (
                    (long)call.Key!,
                    (CurrencyKind)call.Args[0]!,
                    (int)call.Args[1]!,
                    (string)call.Args[2]!
                )
            );

            return Task.FromResult(WalletCreditResult.Applied);
        };
        fakes.Handlers["GrantFurnitureAsync"] = call =>
        {
            _furniture.Add(((long)call.Key!, (int)call.Args[0]!));

            return Task.FromResult<FurnitureItemSnapshot?>(
                (FurnitureItemSnapshot)
                    RuntimeHelpers.GetUninitializedObject(typeof(FurnitureItemSnapshot))
            );
        };
        fakes.Handlers["GiveBadgeAsync"] = _ => Task.FromResult(true);
        fakes.Handlers["GetCurrencyType"] = call =>
            (int)call.Args[0]! == DUCKETS_ROW
                ? new CurrencyTypeSnapshot
                {
                    Id = DUCKETS_ROW,
                    Name = "duckets",
                    CurrencyType = CurrencyType.ActivityPoints,
                    ActivityPointType = 0,
                    Enabled = true,
                }
                : null;

        _vouchers = new VoucherService(
            _catalog.Db,
            Options.Create(new CatalogConfig()),
            fakes.Create<IGrainFactory>(),
            fakes.Create<ICurrencyTypeProvider>(),
            _time,
            NullLogger<VoucherService>.Instance
        );
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task A_voucher_redeemed_gives_its_rewards_and_names_its_furniture()
    {
        await _vouchers.SaveAsync(
            Voucher("summer26") with
            {
                CurrencyTypeId = DUCKETS_ROW,
                CurrencyAmount = 50,
                FurnitureDefinitionId = CHAIR,
                FurnitureQuantity = 2,
                BadgeCode = "SUMMER",
            },
            Ct
        );

        // Typed in any case, with spaces.
        var reply = await RedeemAsync(ALICE, " sum mer26 ");

        reply.Should().Be(Ok("chair", ""), "the description comes first, the name second");
        _credits
            .Should()
            .BeEquivalentTo([
                ((long)ALICE, CurrencyKind.Credits, 100, $"voucher:{VoucherId()}:credits"),
                (
                    (long)ALICE,
                    CurrencyKind.ActivityPoints(0),
                    50,
                    $"voucher:{VoucherId()}:currency"
                ),
            ]);
        _furniture.Should().Equal((ALICE, CHAIR), (ALICE, CHAIR));
        _catalog
            .Fakes.Log.Of("GiveBadgeAsync")
            .Should()
            .ContainSingle()
            .Which.Args[0]
            .Should()
            .Be("SUMMER");
    }

    [Fact]
    public async Task A_player_redeems_a_voucher_once_and_a_voucher_is_not_used_past_its_limit()
    {
        await _vouchers.SaveAsync(Voucher("ONCE") with { MaxUses = 1 }, Ct);
        await _vouchers.SaveAsync(Voucher("TWICE"), Ct);

        (await RedeemAsync(ALICE, "TWICE")).Should().Be(Ok("", ""));
        (await RedeemAsync(ALICE, "TWICE"))
            .Should()
            .Be(Error("0"), "a player redeems a voucher once");
        (await RedeemAsync(BOB, "TWICE")).Should().Be(Ok("", ""));

        (await RedeemAsync(ALICE, "ONCE")).Should().Be(Ok("", ""));
        (await RedeemAsync(BOB, "ONCE")).Should().Be(Error("0"), "it was used up");

        var (found, _, _) = await _vouchers.SearchAsync("ONCE", 0, Ct);

        found.Single().Uses.Should().Be(1);
        (await _vouchers.GetRedemptionsAsync(found.Single().Id, Ct))
            .Select(x => x.PlayerName)
            .Should()
            .Equal("alice");
        _credits.Should().HaveCount(3);
    }

    [Fact]
    public async Task An_unknown_turned_off_or_expired_code_is_refused()
    {
        await _vouchers.SaveAsync(Voucher("OFF") with { Enabled = false }, Ct);
        await _vouchers.SaveAsync(Voucher("OLD") with { ExpiresAt = NOW.AddMinutes(-1) }, Ct);

        (await RedeemAsync(ALICE, "NOPE")).Should().Be(Error("0"));
        (await RedeemAsync(ALICE, "OFF")).Should().Be(Error("0"));
        (await RedeemAsync(ALICE, "OLD")).Should().Be(Error("0"));
        _credits.Should().BeEmpty();
    }

    [Fact]
    public async Task A_player_who_keeps_getting_codes_wrong_is_stopped_for_the_hour()
    {
        await _vouchers.SaveAsync(Voucher("REAL"), Ct);

        for (var i = 0; i < new VoucherConfig().FailuresPerHour; i++)
            await RedeemAsync(ALICE, $"GUESS{i}");

        (await RedeemAsync(ALICE, "REAL"))
            .Should()
            .Be(Error("0"), "even a real code, until the hour is up");
        (await RedeemAsync(BOB, "REAL")).Should().Be(Ok("", ""), "others aren't stopped");

        _time.Advance(TimeSpan.FromHours(1));

        (await RedeemAsync(ALICE, "REAL")).Should().Be(Ok("", ""));
    }

    [Fact]
    public async Task Generated_codes_are_unique_and_never_hold_the_letters_players_are_told_they_dont()
    {
        var made = await _vouchers.GenerateAsync(Voucher(""), 200, "xmas-", Ct);

        made.Should().HaveCount(200);
        made.Select(x => x.Code).Should().OnlyHaveUniqueItems();
        made.Should().OnlyContain(x => x.Code.StartsWith("XMAS-") && x.Code.Length == 15);
        made.SelectMany(x => x.Code[5..]).Should().NotContain(['I', 'L', 'O', 'W', '0', '1']);
    }

    [Theory]
    [InlineData("has space!", 100, null)]
    [InlineData("NOTHING", 0, null)]
    [InlineData("NOFURNI", 0, 999)]
    public async Task A_voucher_it_cant_give_is_refused(string code, int credits, int? furniture)
    {
        var save = () =>
            _vouchers.SaveAsync(
                Voucher(code) with
                {
                    Credits = credits,
                    FurnitureDefinitionId = furniture,
                    FurnitureQuantity = furniture is null ? 0 : 1,
                },
                Ct
            );

        await save.Should().ThrowAsync<ArgumentException>();
    }

    private int VoucherId() => _vouchers.SearchAsync(null, 0, Ct).Result.Vouchers.Single().Id;

    private static VoucherSnapshot Voucher(string code) =>
        new()
        {
            Id = 0,
            Code = code,
            Credits = 100,
            CurrencyAmount = 0,
            FurnitureQuantity = 0,
            Uses = 0,
            Enabled = true,
            Note = "",
            CreatedAt = default,
        };

    private static (bool Ok, string First, string Second) Ok(string name, string description) =>
        (true, description, name);

    private static (bool Ok, string First, string Second) Error(string code) => (false, code, "");

    /// <summary>The reply as the client reads it: OK with the description then the name, or an error code.</summary>
    private async Task<(bool Ok, string First, string Second)> RedeemAsync(int player, string code)
    {
        var harness = new PacketHarness();

        harness.Resolver.Overrides[typeof(IVoucherService)] = _vouchers;

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("RedeemVoucherMessageEvent"),
            PacketHarness.Payload(w => w.String(code)),
            playerId: player
        );
        var packet = replies.Single();

        if (packet.Header == PacketHarness.Outgoing("VoucherRedeemOkMessageComposer"))
            return (true, packet.PopString(), packet.PopString());

        packet.Header.Should().Be(PacketHarness.Outgoing("VoucherRedeemErrorMessageComposer"));

        return (false, packet.PopString(), "");
    }
}
