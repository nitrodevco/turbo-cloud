using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

/// <summary>
/// Records are kept as long as the hotel set for their kind, then removed: every log and receipt
/// table grew for ever. A respect still pending is never removed, being the recovery service's
/// work, and a catalogue backup made by hand is never removed either.
/// </summary>
public sealed class DataRetentionTests : IDisposable
{
    private static readonly DateTime NOW = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OLD = NOW.AddDays(-400);
    private static readonly DateTime RECENT = NOW.AddDays(-1);

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Records_past_their_keeping_period_go_and_the_rest_stay()
    {
        foreach (var (id, at) in new[] { (1L, OLD), (2L, RECENT) })
        {
            _db.Insert(
                new RoomChatlogEntity
                {
                    Id = id,
                    RoomEntityId = 1,
                    PlayerEntityId = 1,
                    Message = "hi",
                    CreatedAt = at,
                }
            );
            _db.Insert(
                new PlayerCurrencyLedgerEntity
                {
                    Id = id,
                    PlayerEntityId = 1,
                    CurrencyTypeId = 1,
                    Change = 5,
                    BalanceAfter = 5,
                    Reason = WalletChangeReason.Staff,
                    CreatedAt = at,
                }
            );
            _db.Insert(
                new PlayerLoginEntity
                {
                    Id = id,
                    PlayerEntityId = 1,
                    CreatedAt = at,
                }
            );
        }

        _db.Insert(Respect(TestIds.Of("old given"), RespectStatus.Given, OLD));
        _db.Insert(Respect(TestIds.Of("old pending"), RespectStatus.Pending, OLD));
        _db.Insert(Respect(TestIds.Of("recent"), RespectStatus.Given, RECENT));

        await Retention(new RetentionConfig { BatchSize = 1 }).PurgeAsync(Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.Chatlogs.Select(x => x.Id).ToListAsync(Ct)).Should().Equal(2L);
        (await db.PlayerCurrencyLedger.Select(x => x.Id).ToListAsync(Ct)).Should().Equal(2L);
        (await db.PlayerLogins.Select(x => x.Id).ToListAsync(Ct)).Should().Equal(2L);
        (await db.PlayerRespects.Select(x => x.Id).ToListAsync(Ct))
            .Should()
            .BeEquivalentTo([TestIds.Of("old pending"), TestIds.Of("recent")]);
    }

    [Fact]
    public async Task A_kind_set_to_0_days_is_kept_for_good()
    {
        _db.Insert(
            new RoomChatlogEntity
            {
                Id = 1,
                RoomEntityId = 1,
                PlayerEntityId = 1,
                Message = "hi",
                CreatedAt = OLD,
            }
        );

        await Retention(new RetentionConfig { ChatDays = 0 }).PurgeAsync(Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.Chatlogs.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task Only_the_newest_automatic_catalogue_backups_are_kept_and_none_made_by_hand_go()
    {
        for (var id = 1; id <= 4; id++)
            _db.Insert(
                new CatalogBackupEntity
                {
                    Id = id,
                    Name = $"backup {id}",
                    Automatic = id != 1,
                    Data = [],
                }
            );

        await Retention(new RetentionConfig { AutomaticCatalogBackupsKept = 2 }).PurgeAsync(Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.CatalogBackups.Select(x => x.Id).OrderBy(x => x).ToListAsync(Ct))
            .Should()
            .Equal(1, 3, 4);
    }

    private DataRetentionService Retention(RetentionConfig config) =>
        new(
            _db,
            Options.Create(config),
            new ManualTimeProvider(new DateTimeOffset(NOW)),
            NullLogger<DataRetentionService>.Instance
        );

    private static PlayerRespectEntity Respect(Guid id, RespectStatus status, DateTime at) =>
        new()
        {
            Id = id,
            GiverId = 1,
            ReceiverId = 2,
            Status = status,
            CreatedAt = at,
        };
}
