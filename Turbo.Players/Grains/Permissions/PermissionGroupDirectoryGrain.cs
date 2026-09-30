using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Context;
using Turbo.Database.Entities.Permissions;
using Turbo.Database.Extensions;
using Turbo.Players.Configuration;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

/// <summary>
/// Every permission group, one grain for the hotel. Write-through: each change is saved and
/// audited before the in-memory snapshot moves, so there is nothing to flush on deactivation.
/// After a change the whole snapshot is replaced and pushed to every active
/// <see cref="IPlayerPermissionGrain"/>, which re-resolves from memory. Group edits are rare
/// operator actions, so telling every subscriber rather than working out who is affected costs
/// nothing that matters. Kept alive because every player permission grain reads it on
/// activation.
/// </summary>
[KeepAlive]
internal sealed partial class PermissionGroupDirectoryGrain : Grain, IPermissionGroupDirectoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PermissionConfig _permissionConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly IPermissionRegistryProvider _permissionRegistryProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IPermissionGroupDirectoryGrain> _logger;

    private readonly PermissionGroupDirectoryLiveState _state = new();

    private IGrainTimer? _expiryTimer;
    private System.Action? _onRegistryChanged;

    public PermissionGroupDirectoryGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        IPermissionRegistryProvider permissionRegistryProvider,
        TimeProvider timeProvider,
        ILogger<IPermissionGroupDirectoryGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _permissionConfig = playerConfig.Value.Permissions;
        _grainFactory = grainFactory;
        _permissionRegistryProvider = permissionRegistryProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate the permission group directory");

            throw;
        }

        // Idle until ScheduleExpiry sets it; an assignment already past its expiry makes it fire
        // straight away, which is what cleans up after a restart.
        _expiryTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) =>
                await ((PermissionGroupDirectoryGrain)self!).SweepExpiredAsync(ct),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );

        ScheduleExpiry();

        // Raised on the thread that loaded the plugin, outside this grain, so it comes back in as
        // a call. Player grains subscribe through this activation, so while it is inactive there
        // is nobody to tell.
        var self = this.AsReference<IPermissionGroupDirectoryGrain>();

        _onRegistryChanged = () =>
            self.OnRegistryChangedAsync(CancellationToken.None)
                .LogAndForget(_logger, "push a permission registry change");
        _permissionRegistryProvider.Changed += _onRegistryChanged;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _expiryTimer?.Dispose();
        _expiryTimer = null;

        if (_onRegistryChanged is not null)
            _permissionRegistryProvider.Changed -= _onRegistryChanged;

        _onRegistryChanged = null;

        return Task.CompletedTask;
    }

    public Task<PermissionGroupDirectorySnapshot> GetSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_state.Snapshot);

    public Task SubscribeAsync(PlayerId playerId, CancellationToken ct)
    {
        _state.Subscribers.Add(playerId);

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(PlayerId playerId, CancellationToken ct)
    {
        _state.Subscribers.Remove(playerId);

        return Task.CompletedTask;
    }

    public async Task<int> ReloadAsync(CancellationToken ct)
    {
        await HydrateAsync(ct);

        // Not awaited, as with a push: one player failing to read its rows keeps its old ones
        // until its next reload or activation, and must not stop the rest.
        foreach (var playerId in _state.Subscribers)
            _grainFactory
                .GetPlayerPermissionGrain(playerId)
                .ReloadAsync(CancellationToken.None)
                .LogAndForget(_logger, "reload the permissions of player {PlayerId}", playerId);

        _logger.LogInformation(
            "Reloaded {GroupCount} permission groups and {PlayerCount} active players from the database",
            _state.Snapshot.Groups.Count,
            _state.Subscribers.Count
        );

        return _state.Subscribers.Count;
    }

    public Task OnRegistryChangedAsync(CancellationToken ct)
    {
        // Not awaited, as with a group push: one player grain failing must not stop the rest.
        foreach (var playerId in _state.Subscribers)
            _grainFactory
                .GetPlayerPermissionGrain(playerId)
                .OnRegistryChangedAsync(CancellationToken.None)
                .LogAndForget(
                    _logger,
                    "push a permission registry change to player {PlayerId}",
                    playerId
                );

        return Task.CompletedTask;
    }

    private PermissionGroupSnapshot? FindGroup(string name) =>
        _state.Snapshot.Groups.Values.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.Ordinal)
        );

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await GroupsQuery(dbCtx).ToListAsync(ct);

        Publish(entities.Select(x => x.ToSnapshot()).ToImmutableDictionary(x => x.Id));
    }

    private static IQueryable<PermissionGroupEntity> GroupsQuery(TurboDbContext dbCtx) =>
        dbCtx
            .PermissionGroups.AsNoTracking()
            .Include(x => x.Parents)
            .Include(x => x.Nodes)
            .Include(x => x.Meta)
            .AsSplitQuery();

    /// <summary>Replaces the snapshot and tells every active player permission grain.</summary>
    private void Publish(ImmutableDictionary<int, PermissionGroupSnapshot> groups)
    {
        var snapshot = new PermissionGroupDirectorySnapshot
        {
            Version = _state.Snapshot.Version + 1,
            Groups = groups,
        };

        _state.Snapshot = snapshot;

        ScheduleExpiry();

        // Not awaited: a player grain resolving must not hold up the next group edit, and one
        // that fails keeps its previous groups until the next push or its next activation.
        foreach (var playerId in _state.Subscribers)
            _grainFactory
                .GetPlayerPermissionGrain(playerId)
                .OnGroupsChangedAsync(snapshot, CancellationToken.None)
                .LogAndForget(_logger, "push permission groups to player {PlayerId}", playerId);
    }
}
