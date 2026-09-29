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
using Turbo.Database.Extensions;
using Turbo.Events;
using Turbo.Players.Configuration;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

/// <summary>
/// One player's permissions. Write-through: each change is saved and audited before the
/// in-memory rows move, so there is nothing to flush on deactivation. The resolved set is worked
/// out again whenever the player's rows change, the directory pushes new groups, the registry
/// changes (a plugin loaded or unloaded; pushed by the directory, and checked on every read
/// besides), or an assignment runs out.
/// While active it is subscribed to the directory.
/// </summary>
internal sealed partial class PlayerPermissionGrain : Grain, IPlayerPermissionGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PermissionConfig _permissionConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly IPermissionRegistryProvider _permissionRegistryProvider;
    private readonly EventSystem _eventSystem;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IPlayerPermissionGrain> _logger;

    private readonly PlayerPermissionLiveState _state;

    private IGrainTimer? _expiryTimer;

    private PlayerId PlayerId => _state.PlayerId;

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public PlayerPermissionGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        IPermissionRegistryProvider permissionRegistryProvider,
        EventSystem eventSystem,
        TimeProvider timeProvider,
        ILogger<IPlayerPermissionGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _permissionConfig = playerConfig.Value.Permissions;
        _grainFactory = grainFactory;
        _permissionRegistryProvider = permissionRegistryProvider;
        _eventSystem = eventSystem;
        _timeProvider = timeProvider;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);

            var directory = _grainFactory.GetPermissionGroupDirectoryGrain();

            _state.Groups = await directory.GetSnapshotAsync(ct);

            await directory.SubscribeAsync(PlayerId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to hydrate the permissions of player {PlayerId}",
                PlayerId
            );

            throw;
        }

        // Idle until Resolve sets it; an assignment already past its expiry makes it fire
        // straight away, which is what sweeps rows that ran out while the player was away.
        _expiryTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((PlayerPermissionGrain)self!).SweepExpiredAsync(ct),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );

        Resolve();

        // Taken as what the client already knows: it is sent at login, and a grain collected while
        // its player stayed online was reactivated by a change that is compared against it. An
        // expiry missed while inactive is swept, and sent, as soon as the timer set above fires.
        _state.SentClient = _state.Client;
        _state.SentRoom = _state.Resolved;
        _state.Announced = _state.Resolved;
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _expiryTimer?.Dispose();
        _expiryTimer = null;

        try
        {
            await _grainFactory.GetPermissionGroupDirectoryGrain().UnsubscribeAsync(PlayerId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to unsubscribe the permissions of player {PlayerId} from the group directory",
                PlayerId
            );
        }
    }

    public Task<bool> HasAsync(string node, CancellationToken ct)
    {
        var resolved = EnsureResolved();

        if (!_state.Registry!.IsCheckable(node))
        {
            // A gate asking for a node nobody registered is a bug in the gate, not a denial.
            _logger.LogWarning(
                "Permission check for unregistered node {Node} on player {PlayerId}; denied",
                node,
                PlayerId
            );

            return Task.FromResult(false);
        }

        var held = resolved.Has(node);

        if (resolved.IsWatched(node))
            _logger.LogInformation(
                "Verbose: player {PlayerId} checked {Node}: {Held}",
                PlayerId,
                node,
                held
            );

        return Task.FromResult(held);
    }

    public Task<string?> GetMetaAsync(string key, CancellationToken ct)
    {
        var resolved = EnsureResolved();
        var value = resolved.Meta.GetValueOrDefault(key);

        if (resolved.IsWatched(key))
            _logger.LogInformation(
                "Verbose: player {PlayerId} read meta {Key}: {Value}",
                PlayerId,
                key,
                value ?? "(unset)"
            );

        return Task.FromResult(value);
    }

    public Task<ResolvedPermissionsSnapshot> GetResolvedAsync(CancellationToken ct) =>
        Task.FromResult(EnsureResolved());

    public Task<PermissionClientSnapshot> GetClientStateAsync(CancellationToken ct)
    {
        EnsureResolved();

        return Task.FromResult(_state.Client!);
    }

    public Task<PermissionCheckSnapshot> ExplainAsync(string node, CancellationToken ct)
    {
        EnsureResolved();

        return Task.FromResult(
            PermissionResolver.Explain(
                _state.Registry!,
                _state.Groups.Groups,
                BuildAssignments(),
                node,
                UtcNow
            )
        );
    }

    public Task<PlayerPermissionAssignmentsSnapshot> GetAssignmentsAsync(CancellationToken ct)
    {
        var now = UtcNow;
        var all = BuildAssignments();

        return Task.FromResult(
            new PlayerPermissionAssignmentsSnapshot
            {
                Groups = [.. all.Groups.Where(x => IsLive(x.ExpiresAt, now))],
                Nodes = [.. all.Nodes.Where(x => IsLive(x.ExpiresAt, now))],
                Meta = [.. all.Meta.Where(x => IsLive(x.ExpiresAt, now))],
            }
        );
    }

    public async Task<ImmutableArray<PermissionAuditSnapshot>> GetAuditAsync(
        int count,
        CancellationToken ct
    )
    {
        var take = Math.Clamp(count, 1, _permissionConfig.AuditPageLimit);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = await dbCtx
            .PermissionAudit.AsNoTracking()
            .Where(x =>
                x.TargetType == PermissionAuditTargetType.Player && x.TargetId == PlayerId.Value
            )
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(ct);

        return [.. rows.Select(x => x.ToSnapshot())];
    }

    public async Task SetVerboseAsync(string? filter, CancellationToken ct)
    {
        if (_state.VerboseFilter == filter)
            return;

        _state.VerboseFilter = filter;

        Resolve();

        _logger.LogInformation(
            "Verbose permission checks for player {PlayerId}: {Filter}",
            PlayerId,
            filter is null ? "off"
                : filter.Length == 0 ? "every node"
                : filter + "*"
        );

        await PublishChangesAsync(force: false, ct);
    }

    public async Task ReloadAsync(CancellationToken ct)
    {
        await HydrateAsync(ct);

        Resolve();

        _logger.LogInformation("Reloaded the permissions of player {PlayerId}", PlayerId);

        await PublishChangesAsync(force: false, ct);
    }

    public async Task OnGroupsChangedAsync(
        PermissionGroupDirectorySnapshot groups,
        CancellationToken ct
    )
    {
        // Pushes are not awaited by the directory, so an older one can arrive after a newer one.
        if (groups.Version <= _state.Groups.Version)
            return;

        _state.Groups = groups;

        // A deleted group's memberships were cascaded away in the database; forget them here too.
        foreach (var key in _state.MembershipsByGroupId.Keys.ToList())
        {
            if (!groups.Groups.ContainsKey(key.GroupId))
                _state.MembershipsByGroupId.Remove(key);
        }

        Resolve();

        await PublishChangesAsync(force: false, ct);
    }

    public async Task OnRegistryChangedAsync(CancellationToken ct)
    {
        // A plugin reload unloads and loads close together; a push that finds the registry
        // already resolved against does nothing.
        if (ReferenceEquals(_state.Registry, _permissionRegistryProvider.Current))
            return;

        Resolve();

        await PublishChangesAsync(force: false, ct);
    }

    private PermissionGroupSnapshot? FindGroup(string name) =>
        _state.Groups.Groups.Values.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.Ordinal)
        );

    /// <summary>The resolved set, worked out again first if the registry moved or something ran out.</summary>
    private ResolvedPermissionsSnapshot EnsureResolved()
    {
        if (
            _state.Resolved is not { } resolved
            || !ReferenceEquals(_state.Registry, _permissionRegistryProvider.Current)
            || resolved.NextExpiresAt <= UtcNow
        )
        {
            resolved = Resolve();

            // Reached from a read, which must not wait on a send; the client hears of a change
            // a plugin load or an expiry made as soon as it can.
            PublishChangesAsync(force: false, CancellationToken.None)
                .LogAndForget(_logger, "send the permissions of player {PlayerId}", PlayerId);
        }

        return resolved;
    }

    private ResolvedPermissionsSnapshot Resolve()
    {
        var registry = _permissionRegistryProvider.Current;
        var resolved = PermissionResolver.Resolve(
            registry,
            _state.Groups.Groups,
            BuildAssignments(),
            UtcNow
        ) with
        {
            VerboseFilter = _state.VerboseFilter,
        };

        _state.Registry = registry;
        _state.Resolved = resolved;
        _state.Client = PermissionProjection.Project(registry, resolved);

        if (resolved.UnregisteredNodes.Length > 0)
            _logger.LogDebug(
                "Player {PlayerId} is assigned unregistered permission nodes {Nodes}",
                PlayerId,
                resolved.UnregisteredNodes
            );

        ScheduleExpiry(resolved.NextExpiresAt);

        return resolved;
    }

    private PlayerPermissionAssignmentsSnapshot BuildAssignments() =>
        new()
        {
            Groups = [.. _state.MembershipsByGroupId.Values],
            Nodes = [.. _state.NodesByNode.Values],
            Meta = [.. _state.MetaByKey.Values],
        };

    private static bool IsLive(DateTime? expiresAt, DateTime now) =>
        expiresAt is null || expiresAt > now;

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var groups = await dbCtx
            .PlayerPermissionGroups.AsNoTracking()
            .Where(x => x.PlayerEntityId == PlayerId.Value)
            .ToListAsync(ct);
        var nodes = await dbCtx
            .PlayerPermissionNodes.AsNoTracking()
            .Where(x => x.PlayerEntityId == PlayerId.Value)
            .ToListAsync(ct);
        var meta = await dbCtx
            .PlayerPermissionMeta.AsNoTracking()
            .Where(x => x.PlayerEntityId == PlayerId.Value)
            .ToListAsync(ct);

        _state.MembershipsByGroupId.Clear();
        _state.NodesByNode.Clear();
        _state.MetaByKey.Clear();

        foreach (var row in groups)
            _state.MembershipsByGroupId[(row.GroupEntityId, row.IsTemporary)] = row.ToSnapshot();

        foreach (var row in nodes)
            _state.NodesByNode[(row.Node, row.IsTemporary)] = row.ToSnapshot();

        foreach (var row in meta)
            _state.MetaByKey[(row.Key, row.IsTemporary)] = row.ToSnapshot();
    }
}
