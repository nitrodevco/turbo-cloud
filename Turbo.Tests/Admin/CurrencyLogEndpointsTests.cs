using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Movements;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's currency log (<c>/api/currency-log</c>): staff who see players read every change
/// to a wallet the ledger kept, named, narrowed to a player, a currency or a cause, with a
/// player's in and out per currency; the answer says whether the ledger is being kept.
/// </summary>
public sealed class CurrencyLogEndpointsTests : IAsyncDisposable
{
    private const int ALICE = 2;
    private const int BOB = 3;
    private const int CREDITS = 1;
    private const int DUCKETS = 2;

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly TestOptionsMonitor<MovementsConfig> _movements = new(
        new MovementsConfig { Enabled = true }
    );
    private readonly int _port;
    private readonly IHostedService _server;

    private Func<string, bool> _holds = _ => true;

    public CurrencyLogEndpointsTests()
    {
        _port = TestPorts.Free();

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();
        _fakes.Instances[(typeof(IOptionsMonitor<MovementsConfig>), null)] = _movements;

        _db.Insert(Player(ALICE, "Alice"));
        _db.Insert(Player(BOB, "Bob"));
        _db.Insert(Currency(CREDITS, "Credits", CurrencyType.Credits, null));
        _db.Insert(Currency(DUCKETS, "Duckets", CurrencyType.ActivityPoints, 0));
        _db.Insert(Entry(1, ALICE, -25, "catalog.purchase", CurrencyType.Credits, null));
        _db.Insert(
            Entry(2, ALICE, 100, "staff.give_currency", CurrencyType.ActivityPoints, 0, actor: BOB)
        );
        _db.Insert(
            Entry(
                3,
                BOB,
                40,
                "market.redeem",
                CurrencyType.Credits,
                null,
                other: PlaceKind.MarketplaceOffer,
                place: 77
            )
        );
        _db.Insert(
            Entry(
                4,
                ALICE,
                30,
                "market.redeem",
                CurrencyType.Credits,
                null,
                other: PlaceKind.MarketplaceOffer,
                place: 78
            )
        );
        _db.Insert(
            Entry(
                5,
                ALICE,
                -10,
                "market.buy",
                CurrencyType.Credits,
                null,
                other: PlaceKind.MarketplaceOffer,
                place: 79
            )
        );

        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync(CancellationToken.None);
        _db.Dispose();
    }

    [Fact]
    public async Task A_players_changes_are_named_newest_first_with_their_totals()
    {
        await _server.StartAsync(Ct);

        var log = await GetAsync("/api/currency-log?player=alice");

        log.GetProperty("recording").GetBoolean().Should().BeTrue();
        Ids(log).Should().Equal(5, 4, 2, 1);
        var gift = log.GetProperty("entries")[2];
        gift.GetProperty("playerName").GetString().Should().Be("Alice");
        gift.GetProperty("currency").GetString().Should().Be("Duckets");
        gift.GetProperty("amount").GetInt64().Should().Be(100);
        gift.GetProperty("actorName").GetString().Should().Be("Bob");
        var sale = log.GetProperty("entries")[1];
        sale.GetProperty("other").GetString().Should().Be("offer");
        sale.GetProperty("otherPlaceId").GetInt64().Should().Be(78);
        log.GetProperty("totals")
            .EnumerateArray()
            .Select(x =>
                (
                    x.GetProperty("currency").GetString(),
                    x.GetProperty("in").GetInt64(),
                    x.GetProperty("out").GetInt64()
                )
            )
            .Should()
            .Equal(("Credits", 30L, 35L), ("Duckets", 100L, 0L));
    }

    [Fact]
    public async Task The_log_narrows_to_a_cause_and_its_kin_and_to_a_currency()
    {
        await _server.StartAsync(Ct);

        Ids(await GetAsync("/api/currency-log?cause=market")).Should().Equal(5, 4, 3);
        Ids(await GetAsync("/api/currency-log?cause=market.redeem")).Should().Equal(4, 3);
        Ids(await GetAsync($"/api/currency-log?currency={DUCKETS}")).Should().Equal(2);
        (await GetAsync("/api/currency-log"))
            .GetProperty("totals")
            .ValueKind.Should()
            .Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_log_says_when_the_ledger_is_not_kept_and_is_only_for_staff_who_see_players()
    {
        _movements.Set(new MovementsConfig { Enabled = false });
        await _server.StartAsync(Ct);

        (await GetAsync("/api/currency-log"))
            .GetProperty("recording")
            .GetBoolean()
            .Should()
            .BeFalse();

        _holds = node => node != PermissionNodes.Admin.PLAYERS_VIEW;

        (await SendAsync("/api/currency-log")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static long[] Ids(JsonElement page) =>
        [
            .. page.GetProperty("entries")
                .EnumerateArray()
                .Select(x => x.GetProperty("id").GetInt64()),
        ];

    private static CurrencyLedgerEntryEntity Entry(
        long id,
        int player,
        long amount,
        string cause,
        CurrencyType type,
        int? points,
        int actor = 0,
        PlaceKind other = PlaceKind.None,
        long place = 0
    ) =>
        new()
        {
            Id = id,
            OutboxId = id,
            OperationId = $"op-{id}",
            Seq = 0,
            OccurredAt = DateTime.UtcNow.AddMinutes(id - 10),
            PlayerId = player,
            CurrencyType = type,
            ActivityPointType = points,
            Amount = amount,
            Cause = cause,
            ActorPlayerId = actor,
            OtherKind = other,
            OtherPlaceId = place,
        };

    private static CurrencyTypeEntity Currency(
        int id,
        string name,
        CurrencyType type,
        int? points
    ) =>
        new()
        {
            Id = id,
            Name = name,
            CurrencyType = type,
            ActivityPointType = points,
            Enabled = true,
        };

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private async Task<HttpResponseMessage> SendAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }

    private async Task<JsonElement> GetAsync(string path) =>
        JsonDocument.Parse(await (await SendAsync(path)).Content.ReadAsStringAsync(Ct)).RootElement;
}
