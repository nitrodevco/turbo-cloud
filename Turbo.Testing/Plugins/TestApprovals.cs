using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Turbo.Contracts.Approvals;
using Turbo.Database.Context;
using Turbo.Database.Entities;
using Turbo.Plugins.Approvals;

namespace Turbo.Testing;

/// <summary>
/// The hotel's approvals (turbo-cloud docs/approvals.md), as the admin panel's inbox plays them:
/// the requests the plugins asked for, approved or denied by a staff member, each refused for the
/// reasons a server gives (their node, their own request, decided already, expired). An approval
/// runs the handler the plugin registered for its kind, as on a server, and a decision waits for
/// the handlers of the events it raised. A request expires as <see cref="TestHotel.Clock"/> moves
/// past it: the next thing asked of the approvals finds it expired, as the server's sweep each
/// minute does. The requests live in the server's own <c>approvals</c> table, in memory.
/// </summary>
public sealed class TestApprovals
{
    private readonly TestHotel _hotel;
    private readonly List<ApprovalDecision> _decisions = [];

    internal TestApprovals(TestHotel hotel) => _hotel = hotel;

    /// <summary>Each decision made here, in order, refused ones too.</summary>
    public IReadOnlyList<ApprovalDecision> Decisions => _decisions;

    /// <summary>The pending requests, newest first; only those of <paramref name="kind"/> when given.</summary>
    public async Task<IReadOnlyList<Approval>> PendingAsync(
        string? kind = null,
        CancellationToken ct = default
    ) => await ListAsync(ApprovalState.Pending, kind, ct).ConfigureAwait(false);

    /// <summary>Every request, newest first, in whatever state; only those of <paramref name="kind"/> when given.</summary>
    public async Task<IReadOnlyList<Approval>> AllAsync(
        string? kind = null,
        CancellationToken ct = default
    ) => await ListAsync(null, kind, ct).ConfigureAwait(false);

    /// <summary>The request <paramref name="id"/>; null when there is none.</summary>
    public Task<Approval?> GetAsync(int id, CancellationToken ct = default) =>
        _hotel.HotelApprovals.GetAsync(id, ct);

    /// <summary>
    /// Asks for <paramref name="request"/> as core (or as the plugin <paramref name="plugin"/>):
    /// a test of a plugin's handler that does not go through what asks for it.
    /// </summary>
    public Task<Approval> RequestAsync(
        ApprovalRequest request,
        string? plugin = null,
        CancellationToken ct = default
    ) => _hotel.HotelApprovals.RequestAsync(request, plugin, ct);

    /// <summary>
    /// <paramref name="by"/> approves request <paramref name="id"/> in the panel: refused unless
    /// they hold its node (<see cref="TestPlayer.WithPermissions"/>) and it is pending and not
    /// theirs; else the handler of its kind carries it out, once.
    /// </summary>
    public async Task<ApprovalDecision> ApproveAsync(
        int id,
        TestPlayer by,
        string? note = null,
        CancellationToken ct = default
    ) =>
        await DecidedAsync(
                await _hotel.HotelApprovals.ApproveAsync(id, by.Id, note, ct).ConfigureAwait(false)
            )
            .ConfigureAwait(false);

    /// <summary>Approves the one pending request of <paramref name="kind"/>, as <see cref="ApproveAsync(int, TestPlayer, string?, CancellationToken)"/>.</summary>
    public async Task<ApprovalDecision> ApproveAsync(
        string kind,
        TestPlayer by,
        string? note = null,
        CancellationToken ct = default
    ) =>
        await ApproveAsync((await OnePendingAsync(kind, ct).ConfigureAwait(false)).Id, by, note, ct)
            .ConfigureAwait(false);

    /// <summary><paramref name="by"/> denies request <paramref name="id"/>, refused as an approval is; nothing is carried out.</summary>
    public async Task<ApprovalDecision> DenyAsync(
        int id,
        TestPlayer by,
        string? note = null,
        CancellationToken ct = default
    ) =>
        await DecidedAsync(
                await _hotel.HotelApprovals.DenyAsync(id, by.Id, note, ct).ConfigureAwait(false)
            )
            .ConfigureAwait(false);

    /// <summary>Denies the one pending request of <paramref name="kind"/>.</summary>
    public async Task<ApprovalDecision> DenyAsync(
        string kind,
        TestPlayer by,
        string? note = null,
        CancellationToken ct = default
    ) =>
        await DenyAsync((await OnePendingAsync(kind, ct).ConfigureAwait(false)).Id, by, note, ct)
            .ConfigureAwait(false);

    internal async Task<IReadOnlyList<Approval>> ListAsync(
        ApprovalState? state,
        string? kind,
        CancellationToken ct
    )
    {
        var found = new List<Approval>();

        // Listing expires what is past its time, as the server's sweep does.
        for (var page = 1; ; page++)
        {
            var list = await _hotel
                .HotelApprovals.ListAsync(
                    new ApprovalQuery
                    {
                        State = state,
                        Kind = kind,
                        Page = page,
                        PageSize = 200,
                    },
                    ct
                )
                .ConfigureAwait(false);

            found.AddRange(list.Entries);

            if (found.Count >= list.Total || list.Entries.IsEmpty)
                break;
        }

        await _hotel.Events.SettledAsync().ConfigureAwait(false);

        return found;
    }

    private async Task<Approval> OnePendingAsync(string kind, CancellationToken ct)
    {
        var pending = await PendingAsync(kind, ct).ConfigureAwait(false);

        return pending.Count == 1
            ? pending[0]
            : throw new HotelAssertionException(
                $"{pending.Count} requests of {kind} are pending, not 1; name one by its id."
            );
    }

    private async Task<ApprovalDecision> DecidedAsync(ApprovalDecision decision)
    {
        _decisions.Add(decision);
        await _hotel.Events.SettledAsync().ConfigureAwait(false);

        return decision;
    }
}

/// <summary>
/// The server's tables in an in-memory SQLite database, for the approvals: the schema is made from
/// the server's model once a run and copied into each hotel's database as it first asks.
/// </summary>
internal sealed class TestServerDatabase : IDbContextFactory<TurboDbContext>, IDisposable
{
    private static readonly Lazy<SqliteConnection> SCHEMA = new(() =>
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var ctx = new SqliteServerContext(OptionsFor(conn));
        ctx.Database.EnsureCreated();

        return conn;
    });

    private readonly Lazy<(
        SqliteConnection Connection,
        DbContextOptions<TurboDbContext> Options
    )> _db = new(() =>
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        lock (SCHEMA)
            SCHEMA.Value.BackupDatabase(conn);

        return (conn, OptionsFor(conn));
    });

    public TurboDbContext CreateDbContext() => new SqliteServerContext(_db.Value.Options);

    public Task<TurboDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
        Task.FromResult(CreateDbContext());

    public void Dispose()
    {
        if (_db.IsValueCreated)
            _db.Value.Connection.Dispose();
    }

    private static DbContextOptions<TurboDbContext> OptionsFor(SqliteConnection conn) =>
        new DbContextOptionsBuilder<TurboDbContext>().UseSqlite(conn).Options;

    /// <summary>
    /// The server's model on SQLite: MySQL's collations go, and the timestamps MySQL fills in are
    /// plain columns (the approvals write their own).
    /// </summary>
    private sealed class SqliteServerContext(DbContextOptions<TurboDbContext> options)
        : TurboDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            foreach (var entityType in mb.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                    property.SetCollation(null);

                if (!typeof(TurboEntity).IsAssignableFrom(entityType.ClrType))
                    continue;

                entityType.FindProperty(nameof(TurboEntity.CreatedAt))!.ValueGenerated =
                    ValueGenerated.Never;

                if (entityType.FindProperty("UpdatedAt") is { } updatedAt)
                    updatedAt.ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}
