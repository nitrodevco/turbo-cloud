using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Operations.Movements;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

/// <summary>
/// The server's currency ledger: each currency movement that leaves or reaches a wallet is kept
/// once, signed, with its cause and the other side, however often it is delivered; nothing else
/// is kept; and it reads the outbox only while movements are recorded.
/// </summary>
public sealed class CurrencyLedgerTests : IDisposable
{
    private const int ALICE = 2;
    private const int BOB = 3;

    private static readonly DateTime NOW = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Each_wallet_change_is_kept_once_signed_and_nothing_else_is()
    {
        var ledger = new CurrencyLedger(_db);
        var batch = Batch(
            12,
            // Alice buys from the catalog: credits leave her wallet.
            Row(
                1,
                Currency("op-buy", 0, Wallet(ALICE), AssetPlace.Nowhere, 25, "catalog.purchase")
            ),
            // Bob is given duckets by staff.
            Row(
                2,
                Currency(
                    "op-give",
                    0,
                    AssetPlace.Nowhere,
                    Wallet(BOB),
                    100,
                    "staff.give_currency",
                    points: 0
                )
            ),
            // Bob's sale revenue off his offer onto his wallet.
            Row(
                3,
                Currency(
                    "op-redeem",
                    1,
                    new AssetPlace(BOB, PlaceKind.MarketplaceOffer, 77, null),
                    Wallet(BOB),
                    40,
                    "market.redeem"
                )
            ),
            // A wallet to another: a row for each.
            Row(4, Currency("op-pay", 0, Wallet(ALICE), Wallet(BOB), 5, "wired.chest")),
            // A furni and a marker are not wallet changes.
            Row(
                5,
                new AssetMovement
                {
                    OperationId = "op-furni",
                    Seq = 0,
                    OccurredAtUtc = NOW,
                    Asset = new AssetRef(AssetKind.Furni, 9, 1, null),
                    From = AssetPlace.Nowhere,
                    To = new AssetPlace(ALICE, PlaceKind.Inventory, 0, null),
                    Cause = "catalog.purchase",
                }
            ),
            Row(
                6,
                new AssetMovement
                {
                    OperationId = "op-marker",
                    Seq = 0,
                    OccurredAtUtc = NOW,
                    Asset = default,
                    From = AssetPlace.Nowhere,
                    To = AssetPlace.Nowhere,
                    Cause = MovementCauses.RECORDING_STARTED,
                }
            )
        );

        await ledger.HandleAsync(batch, Ct);
        // Delivered again, as a dispatcher may: nothing is kept twice.
        await ledger.HandleAsync(batch, Ct);

        await using var db = _db.CreateDbContext();
        db.CurrencyLedger.OrderBy(x => x.OutboxId)
            .ThenBy(x => x.Amount)
            .Select(x => new
            {
                x.PlayerId,
                x.Amount,
                x.Cause,
                x.CurrencyType,
                x.ActivityPointType,
                x.OtherKind,
                x.OtherOwnerId,
                x.OtherPlaceId,
            })
            .ToList()
            .Should()
            .BeEquivalentTo(
                [
                    new
                    {
                        PlayerId = ALICE,
                        Amount = -25L,
                        Cause = "catalog.purchase",
                        CurrencyType = CurrencyType.Credits,
                        ActivityPointType = (int?)null,
                        OtherKind = PlaceKind.None,
                        OtherOwnerId = 0,
                        OtherPlaceId = 0L,
                    },
                    new
                    {
                        PlayerId = BOB,
                        Amount = 100L,
                        Cause = "staff.give_currency",
                        CurrencyType = CurrencyType.ActivityPoints,
                        ActivityPointType = (int?)0,
                        OtherKind = PlaceKind.None,
                        OtherOwnerId = 0,
                        OtherPlaceId = 0L,
                    },
                    new
                    {
                        PlayerId = BOB,
                        Amount = 40L,
                        Cause = "market.redeem",
                        CurrencyType = CurrencyType.Credits,
                        ActivityPointType = (int?)null,
                        OtherKind = PlaceKind.MarketplaceOffer,
                        OtherOwnerId = BOB,
                        OtherPlaceId = 77L,
                    },
                    new
                    {
                        PlayerId = ALICE,
                        Amount = -5L,
                        Cause = "wired.chest",
                        CurrencyType = CurrencyType.Credits,
                        ActivityPointType = (int?)null,
                        OtherKind = PlaceKind.Wallet,
                        OtherOwnerId = BOB,
                        OtherPlaceId = 0L,
                    },
                    new
                    {
                        PlayerId = BOB,
                        Amount = 5L,
                        Cause = "wired.chest",
                        CurrencyType = CurrencyType.Credits,
                        ActivityPointType = (int?)null,
                        OtherKind = PlaceKind.Wallet,
                        OtherOwnerId = ALICE,
                        OtherPlaceId = 0L,
                    },
                ],
                options => options.WithStrictOrdering()
            );
        (await ledger.GetCursorAsync(Ct)).Should().Be(12);

        // A late batch from a dispatcher that was behind keeps the cursor where it is.
        await ledger.HandleAsync(Batch(8), Ct);
        (await ledger.GetCursorAsync(Ct)).Should().Be(12);
    }

    [Fact]
    public async Task The_ledger_reads_the_outbox_only_while_movements_are_recorded()
    {
        var registry = new CountingRegistry();
        var config = new LiveMonitor(new MovementsConfig { Enabled = false });
        using var service = new CurrencyLedgerService(new CurrencyLedger(_db), registry, config);

        await service.StartAsync(Ct);
        registry.Live.Should().Be(0);

        config.Set(new MovementsConfig { Enabled = true });
        registry.Live.Should().Be(1);

        config.Set(new MovementsConfig { Enabled = true, CurrencyLedger = false });
        registry.Live.Should().Be(0);

        config.Set(new MovementsConfig { Enabled = true });
        await service.StopAsync(Ct);
        registry.Live.Should().Be(0);
        registry.Registered.Should().Be(2);
    }

    private static AssetPlace Wallet(int player) => new(player, PlaceKind.Wallet, 0, null);

    private static AssetMovement Currency(
        string operation,
        short seq,
        AssetPlace from,
        AssetPlace to,
        long amount,
        string cause,
        int? points = null
    ) =>
        new()
        {
            OperationId = operation,
            Seq = seq,
            OccurredAtUtc = NOW,
            Asset = new AssetRef(
                AssetKind.Currency,
                0,
                0,
                points is { } type ? CurrencyKind.ActivityPoints(type) : CurrencyKind.Credits
            ),
            From = from,
            To = to,
            Amount = amount,
            Cause = cause,
            ActorPlayerId = 1,
        };

    private static OutboxMovement Row(long id, AssetMovement movement) => new(id, movement, NOW);

    private static AssetMovementBatch Batch(long cursor, params OutboxMovement[] rows) =>
        new() { Movements = [.. rows], Cursor = cursor };

    /// <summary>A registry that counts what is registered now and in all.</summary>
    private sealed class CountingRegistry : IAssetMovementConsumerRegistry
    {
        public int Live;
        public int Registered;

        public IDisposable Register(IAssetMovementConsumer consumer)
        {
            Live++;
            Registered++;

            return new Handle(this);
        }

        private sealed class Handle(CountingRegistry owner) : IDisposable
        {
            public void Dispose() => owner.Live--;
        }
    }

    /// <summary>Settings that tell their listeners when they change, as a live setting does.</summary>
    private sealed class LiveMonitor(MovementsConfig value) : IOptionsMonitor<MovementsConfig>
    {
        private readonly List<Action<MovementsConfig, string?>> _listeners = [];

        public MovementsConfig CurrentValue { get; private set; } = value;

        public MovementsConfig Get(string? name) => CurrentValue;

        public void Set(MovementsConfig value)
        {
            CurrentValue = value;

            foreach (var listener in _listeners.ToArray())
                listener(value, null);
        }

        public IDisposable? OnChange(Action<MovementsConfig, string?> listener)
        {
            _listeners.Add(listener);

            return new Stop(() => _listeners.Remove(listener));
        }

        private sealed class Stop(Action stop) : IDisposable
        {
            public void Dispose() => stop();
        }
    }
}
