using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Contracts.Players;
using Turbo.Database.Context;
using Turbo.Events.Players;
using Turbo.Grains.Shared;
using Turbo.Streams;

namespace Turbo.Grains.Players;

public class PlayerGrain : DatabaseGrain<PlayerState, PlayerEventEnvelope, TurboDbContext>, IPlayerGrain
{
    private long PlayerId => this.GetPrimaryKeyLong();

    public PlayerGrain(
        IDbContextFactory<TurboDbContext> dbContextFactory,
        IPersistentState<PlayerState> state,
        ILogger<PlayerGrain> logger)
        : base(dbContextFactory, state, logger)
    {
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await base.OnActivateAsync(ct);

        _stream.OnNextAsync(PlayerEventEnvelope.Create(PlayerId, new PlayerActivatedEvent()));
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        // modify offline
        await base.OnDeactivateAsync(reason, ct);
    }

    protected override async Task HydrateFromDatabaseAsync(CancellationToken ct)
    {
        var id = this.GetPrimaryKeyString();

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity = await db.Players.SingleOrDefaultAsync(e => e.Id.ToString() == id, ct);

        if (entity is null) throw new Exception($"Player with ID {id} not found in database.");

        _state.State.Name = entity.Name;
        _state.State.Motto = entity.Motto ?? string.Empty;
        _state.State.Figure = entity.Figure ?? string.Empty;

        await _state.WriteAsync();

        AcceptChanges();
    }

    protected override async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        var id = this.GetPrimaryKeyString();

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity = await db.Players.SingleOrDefaultAsync(e => e.Id.ToString() == id, ct);

        if (entity is null) throw new Exception($"Player with ID {id} not found in database.");

        entity.Name = _state.State.Name;
        entity.Motto = _state.State.Motto;
        entity.Figure = _state.State.Figure;

        await db.SaveChangesAsync(ct);

        AcceptChanges();
    }

    protected override Task SetupStreamAsync()
    {
        var streamProvider = this.GetStreamProvider(PlayerStreams.ProviderName);

        _stream = streamProvider.GetStream<PlayerEventEnvelope>(PlayerStreams.Id(PlayerId));

        return Task.CompletedTask;
    }

    public Task<PlayerSummary> GetAsync() =>
        Task.FromResult(new PlayerSummary(this.GetPrimaryKeyString(), _state.State.Name, _state.State.Motto, _state.State.Figure));
}