namespace Turbo.Grains.Players;

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

/// <summary>
/// Orleans grain for managing player state and events.
/// </summary>
public class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory;
    private readonly GrainStateHost<PlayerState> _host;
    private readonly ILogger<PlayerGrain> _logger;
    private IAsyncStream<PlayerEventEnvelope> _stream = default!;
    private StreamSubscriptionHandle<PlayerEventEnvelope>? _subHandle;

    /// <summary>
    /// Gets the player ID for this grain.
    /// </summary>
    public long PlayerId => this.GetPrimaryKeyLong();

    public Task<long> GetPlayerId() => Task.FromResult(PlayerId);

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerGrain"/> class.
    /// </summary>
    public PlayerGrain(
        [PersistentState(nameof(PlayerState), "PlayerStore")] IPersistentState<PlayerState> inner,
        IDbContextFactory<TurboDbContext> dbContextFactory,
        ILogger<PlayerGrain> logger)
    {
        _dbContextFactory = dbContextFactory;
        _host = new GrainStateHost<PlayerState>(inner, factory: () => new PlayerState());
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await ActivateStreamAsync();
            await _host.InitializeAsync();
            await HydrateFromExternalAsync(ct);
            await PublishAsync(PlayerEventEnvelope.Create(PlayerId, new PlayerActivatedEvent()));
            _logger.LogInformation("PlayerGrain {PlayerId} activated.", PlayerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating PlayerGrain {PlayerId}", PlayerId);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        try
        {
            await WriteToDatabaseAsync(ct);
            await DeactivateStreamAsync();
            _logger.LogInformation("PlayerGrain {PlayerId} deactivated.", PlayerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating PlayerGrain {PlayerId}", PlayerId);
            throw;
        }
    }

    /// <summary>
    /// Hydrates the player state from the external database if not already initialized.
    /// </summary>
    protected async Task HydrateFromExternalAsync(CancellationToken ct)
    {
        if (_host.State.Initialized)
        {
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity = await db.Players.AsNoTracking().SingleOrDefaultAsync(e => e.Id == PlayerId, ct);

        if (entity is null)
        {
            _host.State.Initialized = true;
            _logger.LogWarning("Player with ID {PlayerId} not found in database.", PlayerId);
            throw new Exception($"Player with ID {PlayerId} not found in database.");
        }
        else
        {
            _host.Replace(new PlayerState
            {
                Name = entity.Name ?? string.Empty,
                Motto = entity.Motto ?? string.Empty,
                Figure = entity.Figure ?? string.Empty,
                Initialized = true,
            });
        }

        _host.AcceptChanges();
    }

    /// <summary>
    /// Writes the player state to the database if dirty.
    /// </summary>
    protected async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        if (!_host.IsDirty)
        {
            return;
        }

        await _host.WriteIfDirtyAsync();

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        await db.Players
            .Where(p => p.Id == PlayerId)
            .ExecuteUpdateAsync(
                up => up
                .SetProperty(p => p.Name, _host.State.Name)
                .SetProperty(p => p.Motto, _host.State.Motto)
                .SetProperty(p => p.Figure, _host.State.Figure),
                ct);
    }

    /// <summary>
    /// Activates the Orleans stream for player events.
    /// </summary>
    protected async Task ActivateStreamAsync()
    {
        var streamProvider = this.GetStreamProvider(PlayerStreams.ProviderName);

        _stream = streamProvider.GetStream<PlayerEventEnvelope>(PlayerStreams.Id(PlayerId));

        var handles = await _stream.GetAllSubscriptionHandles();

        _subHandle = handles.Count > 0
            ? await handles[0].ResumeAsync(OnPlayerEvent)
            : await _stream.SubscribeAsync(OnPlayerEvent);
    }

    /// <summary>
    /// Deactivates the Orleans stream for player events.
    /// </summary>
    protected async Task DeactivateStreamAsync()
    {
        if (_subHandle is not null)
        {
            await _subHandle.UnsubscribeAsync();
        }
    }

    /// <inheritdoc />
    public async Task SetName(string name, CancellationToken ct = default)
    {
        _host.State.Name = name;
        await WriteToDatabaseAsync(ct);
        _logger.LogInformation("PlayerGrain {PlayerId} name set to {Name}.", PlayerId, name);
    }

    /// <summary>
    /// Handles incoming player events from the stream.
    /// </summary>
    private Task OnPlayerEvent(PlayerEventEnvelope evt, StreamSequenceToken? token)
    {
        _logger.LogInformation("Player {PlayerId} got event {Event}", PlayerId, evt.ToString());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<PlayerSummary> GetAsync() =>
        Task.FromResult(new PlayerSummary(PlayerId, _host.State.Name, _host.State.Motto, _host.State.Figure));

    /// <inheritdoc />
    public Task PublishAsync(PlayerEventEnvelope evt)
        => _stream.OnNextAsync(evt);
}
