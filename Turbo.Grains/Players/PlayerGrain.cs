using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Core.Contracts.Players;
using Turbo.Database.Context;
using Turbo.Events.Players;
using Turbo.Grains.Shared;
using Turbo.Streams;

namespace Turbo.Grains.Players;

public class PlayerGrain(
    [PersistentState(nameof(PlayerState), "PlayerStore")]
    IPersistentState<PlayerState> inner,
    IDbContextFactory<TurboDbContext> dbContextFactory) : Grain, IPlayerGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly GrainStateHost<PlayerState> _host = new GrainStateHost<PlayerState>(inner, factory: () => new PlayerState());
    private IAsyncStream<PlayerEventEnvelope> _stream = default!;
    private StreamSubscriptionHandle<PlayerEventEnvelope>? _subHandle;

    private long PlayerId => this.GetPrimaryKeyLong();

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await ActivateStreamAsync();
        await _host.InitializeAsync();
        await HydrateFromExternalAsync(ct);

        await PublishAsync(PlayerEventEnvelope.Create(PlayerId, new PlayerActivatedEvent()));
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        // modify offline
        await WriteToDatabaseAsync(ct);
        await DeactivateStreamAsync();
    }

    protected async Task HydrateFromExternalAsync(CancellationToken ct)
    {
        if (_host.State.Initialized) return;

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity = await db.Players.AsNoTracking().SingleOrDefaultAsync(e => e.Id == PlayerId, ct);

        if (entity is null)
        {
            _host.State.Initialized = true;

            throw new Exception($"Player with ID {PlayerId} not found in database.");
        }
        else
        {
            _host.Replace(new PlayerState
            {
                Name = entity.Name ?? string.Empty,
                Motto = entity.Motto ?? string.Empty,
                Figure = entity.Figure ?? string.Empty,
                Initialized = true
            });
        }

        _host.AcceptChanges();
    }

    protected async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        if (!_host.IsDirty) return;

        await _host.WriteIfDirtyAsync();

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        await db.Players
            .Where(p => p.Id == PlayerId)
            .ExecuteUpdateAsync(up => up
                .SetProperty(p => p.Name, _host.State.Name)
                .SetProperty(p => p.Motto, _host.State.Motto)
                .SetProperty(p => p.Figure, _host.State.Figure),
                ct);
    }

    protected async Task ActivateStreamAsync()
    {
        var streamProvider = this.GetStreamProvider(PlayerStreams.ProviderName);

        _stream = streamProvider.GetStream<PlayerEventEnvelope>(PlayerStreams.Id(PlayerId));

        var handles = await _stream.GetAllSubscriptionHandles();

        _subHandle = handles.Count > 0
            ? await handles[0].ResumeAsync(OnPlayerEvent)
            : await _stream.SubscribeAsync(OnPlayerEvent);
    }

    protected async Task DeactivateStreamAsync()
    {
        if (_subHandle is not null) await _subHandle.UnsubscribeAsync();
    }

    public async Task SetName(string name, CancellationToken ct = default)
    {
        _host.State.Name = name;

        await WriteToDatabaseAsync(ct);
    }

    private Task OnPlayerEvent(PlayerEventEnvelope evt, StreamSequenceToken? token)
    {
        Console.WriteLine($"Player {PlayerId} got event {evt.ToString()}");

        return Task.CompletedTask;
    }

    public Task<PlayerSummary> GetAsync() =>
        Task.FromResult(new PlayerSummary(PlayerId, _host.State.Name, _host.State.Motto, _host.State.Figure));

    public Task PublishAsync(PlayerEventEnvelope evt)
        => _stream.OnNextAsync(evt);
}