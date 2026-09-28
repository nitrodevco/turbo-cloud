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
using Turbo.Events;
using Turbo.Players.Configuration;
using Turbo.Players.Permissions;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Perk;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

/// <summary>
/// One player's permissions. Write-through: each change is saved and audited before the
/// in-memory rows move, so there is nothing to flush on deactivation. The resolved set is worked
/// out again whenever the player's rows change, the directory pushes new groups, the registry
/// changes (a plugin loaded or unloaded; noticed on the next read), or an assignment runs out.
/// While active it is subscribed to the directory.
/// </summary>
internal sealed class PlayerPermissionGrain : Grain, IPlayerPermissionGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PermissionConfig _permissionConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly IPermissionRegistryProvider _permissionRegistryProvider;
    private readonly EventSystem _eventSystem;
    private readonly ILogger<IPlayerPermissionGrain> _logger;

    private readonly PlayerPermissionLiveState _state;

    private IGrainTimer? _expiryTimer;

    private PlayerId PlayerId => _state.PlayerId;

    public PlayerPermissionGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        IPermissionRegistryProvider permissionRegistryProvider,
        EventSystem eventSystem,
        ILogger<IPlayerPermissionGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _permissionConfig = playerConfig.Value.Permissions;
        _grainFactory = grainFactory;
        _permissionRegistryProvider = permissionRegistryProvider;
        _eventSystem = eventSystem;
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

    public Task SendClientStateAsync(CancellationToken ct)
    {
        EnsureResolved();

        return SendClientStateCoreAsync(rights: true, nodes: true, ct);
    }

    public Task SendPermissionNodesAsync(CancellationToken ct)
    {
        EnsureResolved();

        return SendClientStateCoreAsync(rights: false, nodes: true, ct);
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
                DateTime.UtcNow
            )
        );
    }

    public Task<PlayerPermissionAssignmentsSnapshot> GetAssignmentsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
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

    public async Task<PermissionChangeResultType> AddGroupAsync(
        string groupName,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (groupName == PermissionGroupNames.DEFAULT)
            return PermissionChangeResultType.ProtectedGroup;

        var now = DateTime.UtcNow;

        if (expiresAt <= now)
            return PermissionChangeResultType.Expired;

        if (FindGroup(groupName) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        var temporary = expiresAt is not null;

        return await WriteAsync(
            async dbCtx =>
            {
                var row = await dbCtx.PlayerPermissionGroups.FirstOrDefaultAsync(
                    x =>
                        x.PlayerEntityId == PlayerId.Value
                        && x.GroupEntityId == group.Id
                        && x.IsTemporary == temporary,
                    ct
                );

                var until = PermissionExpiry.Resolve(expiresAt, row?.ExpiresAt, mode, now);

                if (row is null)
                {
                    row = new PlayerPermissionGroupEntity
                    {
                        PlayerEntityId = PlayerId.Value,
                        GroupEntityId = group.Id,
                        ExpiresAt = until,
                        IsTemporary = temporary,
                    };

                    dbCtx.PlayerPermissionGroups.Add(row);
                }
                else if (row.ExpiresAt == until)
                    return (PermissionChangeResultType.Unchanged, null);
                else
                    row.ExpiresAt = until;

                dbCtx.PermissionAudit.Add(
                    Audit(PermissionAuditActionType.GroupAdded, groupName, actor, null, until)
                );

                return (
                    PermissionChangeResultType.Changed,
                    () => _state.MembershipsByGroupId[(group.Id, temporary)] = row.ToSnapshot()
                );
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> RemoveGroupAsync(
        string groupName,
        bool temporary,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (groupName == PermissionGroupNames.DEFAULT)
            return PermissionChangeResultType.ProtectedGroup;

        if (FindGroup(groupName) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        return await WriteAsync(
            async dbCtx =>
            {
                var row = await dbCtx.PlayerPermissionGroups.FirstOrDefaultAsync(
                    x =>
                        x.PlayerEntityId == PlayerId.Value
                        && x.GroupEntityId == group.Id
                        && x.IsTemporary == temporary,
                    ct
                );

                if (row is null)
                    return (PermissionChangeResultType.NotFound, null);

                dbCtx.PlayerPermissionGroups.Remove(row);
                dbCtx.PermissionAudit.Add(
                    Audit(
                        PermissionAuditActionType.GroupRemoved,
                        groupName,
                        actor,
                        null,
                        row.ExpiresAt
                    )
                );

                return (
                    PermissionChangeResultType.Changed,
                    () => _state.MembershipsByGroupId.Remove((group.Id, temporary))
                );
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> SetNodeAsync(
        string node,
        bool value,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (!PermissionNodeFormat.IsValidAssignment(node))
            return PermissionChangeResultType.Invalid;

        if (PermissionGroupNames.IsGroupNode(node))
            return PermissionChangeResultType.ReservedNode;

        var now = DateTime.UtcNow;

        if (expiresAt <= now)
            return PermissionChangeResultType.Expired;

        var temporary = expiresAt is not null;

        return await WriteAsync(
            async dbCtx =>
            {
                var row = await dbCtx.PlayerPermissionNodes.FirstOrDefaultAsync(
                    x =>
                        x.PlayerEntityId == PlayerId.Value
                        && x.Node == node
                        && x.IsTemporary == temporary,
                    ct
                );

                var until = PermissionExpiry.Resolve(expiresAt, row?.ExpiresAt, mode, now);

                if (row is null)
                {
                    row = new PlayerPermissionNodeEntity
                    {
                        PlayerEntityId = PlayerId.Value,
                        Node = node,
                        Value = value,
                        ExpiresAt = until,
                        IsTemporary = temporary,
                    };

                    dbCtx.PlayerPermissionNodes.Add(row);
                }
                else if (row.Value == value && row.ExpiresAt == until)
                    return (PermissionChangeResultType.Unchanged, null);
                else
                {
                    row.Value = value;
                    row.ExpiresAt = until;
                }

                dbCtx.PermissionAudit.Add(
                    Audit(
                        PermissionAuditActionType.NodeSet,
                        node,
                        actor,
                        PermissionAuditEntries.Format(value),
                        until
                    )
                );

                return (
                    PermissionChangeResultType.Changed,
                    () => _state.NodesByNode[(node, temporary)] = row.ToSnapshot()
                );
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> UnsetNodeAsync(
        string node,
        bool temporary,
        PlayerId? actor,
        CancellationToken ct
    ) =>
        !PermissionNodeFormat.IsValidAssignment(node)
            ? PermissionChangeResultType.Invalid
            : await WriteAsync(
                async dbCtx =>
                {
                    var row = await dbCtx.PlayerPermissionNodes.FirstOrDefaultAsync(
                        x =>
                            x.PlayerEntityId == PlayerId.Value
                            && x.Node == node
                            && x.IsTemporary == temporary,
                        ct
                    );

                    if (row is null)
                        return (PermissionChangeResultType.NotFound, null);

                    dbCtx.PlayerPermissionNodes.Remove(row);
                    dbCtx.PermissionAudit.Add(
                        Audit(PermissionAuditActionType.NodeUnset, node, actor, null, row.ExpiresAt)
                    );

                    return (
                        PermissionChangeResultType.Changed,
                        () => _state.NodesByNode.Remove((node, temporary))
                    );
                },
                ct
            );

    public async Task<PermissionChangeResultType> SetMetaAsync(
        string key,
        string value,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (!PermissionNodeFormat.IsValidNode(key) || !PermissionNodeFormat.IsValidMetaValue(value))
            return PermissionChangeResultType.Invalid;

        var now = DateTime.UtcNow;

        if (expiresAt <= now)
            return PermissionChangeResultType.Expired;

        var temporary = expiresAt is not null;

        return await WriteAsync(
            async dbCtx =>
            {
                var row = await dbCtx.PlayerPermissionMeta.FirstOrDefaultAsync(
                    x =>
                        x.PlayerEntityId == PlayerId.Value
                        && x.Key == key
                        && x.IsTemporary == temporary,
                    ct
                );

                var until = PermissionExpiry.Resolve(expiresAt, row?.ExpiresAt, mode, now);

                if (row is null)
                {
                    row = new PlayerPermissionMetaEntity
                    {
                        PlayerEntityId = PlayerId.Value,
                        Key = key,
                        Value = value,
                        ExpiresAt = until,
                        IsTemporary = temporary,
                    };

                    dbCtx.PlayerPermissionMeta.Add(row);
                }
                else if (row.Value == value && row.ExpiresAt == until)
                    return (PermissionChangeResultType.Unchanged, null);
                else
                {
                    row.Value = value;
                    row.ExpiresAt = until;
                }

                dbCtx.PermissionAudit.Add(
                    Audit(PermissionAuditActionType.MetaSet, key, actor, value, until)
                );

                return (
                    PermissionChangeResultType.Changed,
                    () => _state.MetaByKey[(key, temporary)] = row.ToSnapshot()
                );
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> UnsetMetaAsync(
        string key,
        bool temporary,
        PlayerId? actor,
        CancellationToken ct
    ) =>
        !PermissionNodeFormat.IsValidNode(key)
            ? PermissionChangeResultType.Invalid
            : await WriteAsync(
                async dbCtx =>
                {
                    var row = await dbCtx.PlayerPermissionMeta.FirstOrDefaultAsync(
                        x =>
                            x.PlayerEntityId == PlayerId.Value
                            && x.Key == key
                            && x.IsTemporary == temporary,
                        ct
                    );

                    if (row is null)
                        return (PermissionChangeResultType.NotFound, null);

                    dbCtx.PlayerPermissionMeta.Remove(row);
                    dbCtx.PermissionAudit.Add(
                        Audit(PermissionAuditActionType.MetaUnset, key, actor, null, row.ExpiresAt)
                    );

                    return (
                        PermissionChangeResultType.Changed,
                        () => _state.MetaByKey.Remove((key, temporary))
                    );
                },
                ct
            );

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

    /// <summary>
    /// Tells whoever needs it about a change: the client when the projection moved, plugins when
    /// a node or meta value moved, and the room the player stands in when a node moved. <paramref name="force"/> tells both even when
    /// nothing looks different, for rows swept after an activation that may have run out while the
    /// grain was collected and its player online.
    /// </summary>
    private async Task PublishChangesAsync(bool force, CancellationToken ct)
    {
        var client = _state.Client!;
        var sent = force ? null : _state.SentClient;
        var rights = sent is null || !client.RightsMatch(sent);
        var nodes = sent is null || !client.NodesMatch(sent);

        if (rights || nodes)
            await SendClientStateCoreAsync(rights, nodes, ct);

        var resolved = _state.Resolved!;

        Announce(resolved);

        if (
            !force
            && _state.SentRoom is { } room
            && room.Granted.SetEquals(resolved.Granted)
            && room.VerboseFilter == resolved.VerboseFilter
        )
            return;

        await _grainFactory
            .GetPlayerPresenceGrain(PlayerId)
            .OnPermissionsChangedAsync(resolved, ct);

        _state.SentRoom = resolved;
    }

    /// <summary>
    /// Raises <see cref="PlayerPermissionsChangedEvent"/> if what the player holds differs from
    /// what was last announced. Not awaited: a handler may call back into this grain, which would
    /// wait on the call raising it.
    /// </summary>
    private void Announce(ResolvedPermissionsSnapshot resolved)
    {
        if (_state.Announced is not { } previous || previous.HoldsSame(resolved))
            return;

        _state.Announced = resolved;

        _eventSystem
            .PublishAsync(
                new PlayerPermissionsChangedEvent
                {
                    PlayerId = PlayerId,
                    Previous = previous,
                    Current = resolved,
                },
                CancellationToken.None
            )
            .LogAndForget(_logger, "announce the permissions of player {PlayerId}", PlayerId);
    }

    /// <summary>
    /// Tells the player's session what changed: <paramref name="rights"/> sends their security
    /// level, ambassador flag and perks, <paramref name="nodes"/> their client-facing nodes, which
    /// the presence passes on only to a session that accepted <c>permission.nodes</c>. The club
    /// level shares the <c>UserRights</c> packet, so it is read from the subscription grain, which
    /// never awaits this one. Sent to an offline player it goes nowhere.
    /// </summary>
    private async Task SendClientStateCoreAsync(bool rights, bool nodes, CancellationToken ct)
    {
        var client = _state.Client!;
        var composers = new List<IComposer>(3);

        if (rights)
        {
            var hasClub = await _grainFactory.HasActiveClubAsync(PlayerId, ct);

            composers.Add(
                new UserRightsMessage
                {
                    ClubLevel = hasClub ? ClubLevelType.Vip : ClubLevelType.None,
                    SecurityLevel = client.SecurityLevel,
                    IsAmbassador = client.IsAmbassador,
                }
            );
            composers.Add(
                new PerkAllowancesMessageComposer
                {
                    Perks =
                    [
                        .. client.Perks.Select(x => new PerkAllowanceItem
                        {
                            Code = PlayerPerkExtensions.ToLegacyString(x.Perk),
                            IsAllowed = x.IsAllowed,
                            ErrorMessage = x.Refusal,
                        }),
                    ],
                }
            );
        }

        // After UserRights, so a client falling back to the level never holds nodes that
        // disagree with a level it has not been sent yet.
        if (nodes)
            composers.Add(new TurboPermissionNodesMessage { Nodes = client.Nodes });

        await _grainFactory.GetPlayerPresenceGrain(PlayerId).SendComposerAsync(composers, ct);

        // What the client knows is the previous state with the parts just sent replaced.
        _state.SentClient = (rights, nodes, _state.SentClient) switch
        {
            (true, true, _) or (_, _, null) => client,
            (true, false, { } sent) => client with { Nodes = sent.Nodes },
            (false, true, { } sent) => sent with { Nodes = client.Nodes },
            _ => _state.SentClient,
        };

        _logger.LogDebug(
            "Sent permissions to player {PlayerId}: security level {SecurityLevel}, ambassador {IsAmbassador}",
            PlayerId,
            client.SecurityLevel,
            client.IsAmbassador
        );
    }

    private PermissionGroupSnapshot? FindGroup(string name) =>
        _state.Groups.Groups.Values.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.Ordinal)
        );

    private PermissionAuditEntity Audit(
        PermissionAuditActionType action,
        string subject,
        PlayerId? actor,
        string? value = null,
        DateTime? expiresAt = null
    ) =>
        PermissionAuditEntries.Create(
            PermissionAuditTargetType.Player,
            PlayerId.Value,
            action,
            subject,
            actor,
            value,
            expiresAt
        );

    /// <summary>
    /// Runs one change against a fresh context. If it changed anything, saves the change and its
    /// audit row in one save, then applies it to memory — only once saved — and resolves.
    /// </summary>
    private async Task<PermissionChangeResultType> WriteAsync(
        Func<TurboDbContext, Task<(PermissionChangeResultType Result, Action? Apply)>> change,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var (result, apply) = await change(dbCtx);

        if (result != PermissionChangeResultType.Changed)
            return result;

        await dbCtx.SaveChangesAsync(ct);

        apply?.Invoke();
        Resolve();

        _logger.LogInformation("Permissions of player {PlayerId} changed", PlayerId);

        await PublishChangesAsync(force: false, ct);

        return result;
    }

    /// <summary>The resolved set, worked out again first if the registry moved or something ran out.</summary>
    private ResolvedPermissionsSnapshot EnsureResolved()
    {
        if (
            _state.Resolved is not { } resolved
            || !ReferenceEquals(_state.Registry, _permissionRegistryProvider.Current)
            || resolved.NextExpiresAt <= DateTime.UtcNow
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
            DateTime.UtcNow
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

    private void ScheduleExpiry(DateTime? next)
    {
        // An expired row the resolver skipped still has to be swept, so the earliest of those
        // counts too.
        var earliestOwn = _state
            .MembershipsByGroupId.Values.Select(x => x.ExpiresAt)
            .Concat(_state.NodesByNode.Values.Select(x => x.ExpiresAt))
            .Concat(_state.MetaByKey.Values.Select(x => x.ExpiresAt))
            .Where(x => x is not null)
            .Min();

        var due = (earliestOwn, next) switch
        {
            (null, null) => (DateTime?)null,
            (null, _) => next,
            (_, null) => earliestOwn,
            _ => earliestOwn < next ? earliestOwn : next,
        };

        if (due is null)
        {
            _expiryTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            return;
        }

        ScheduleExpiryIn(due.Value - DateTime.UtcNow);
    }

    private void ScheduleExpiryIn(TimeSpan due) =>
        _expiryTimer?.Change(
            TimeSpan.FromMilliseconds(
                Math.Clamp(due.TotalMilliseconds, 0, _permissionConfig.ExpiryCheckMaxMs)
            ),
            Timeout.InfiniteTimeSpan
        );

    /// <summary>
    /// The expiry timer: deletes the player's own rows that have run out, audits each, and
    /// resolves (which also drops group assignments that ran out; the directory sweeps those).
    /// A failure keeps the rows, which resolution ignores anyway, and tries again later.
    /// </summary>
    private async Task SweepExpiredAsync(CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;

            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var groups = await dbCtx
                .PlayerPermissionGroups.Where(x =>
                    x.PlayerEntityId == PlayerId.Value && x.ExpiresAt != null && x.ExpiresAt <= now
                )
                .ToListAsync(ct);
            var nodes = await dbCtx
                .PlayerPermissionNodes.Where(x =>
                    x.PlayerEntityId == PlayerId.Value && x.ExpiresAt != null && x.ExpiresAt <= now
                )
                .ToListAsync(ct);
            var meta = await dbCtx
                .PlayerPermissionMeta.Where(x =>
                    x.PlayerEntityId == PlayerId.Value && x.ExpiresAt != null && x.ExpiresAt <= now
                )
                .ToListAsync(ct);

            if (groups.Count + nodes.Count + meta.Count > 0)
            {
                dbCtx.PlayerPermissionGroups.RemoveRange(groups);
                dbCtx.PlayerPermissionNodes.RemoveRange(nodes);
                dbCtx.PlayerPermissionMeta.RemoveRange(meta);

                foreach (var row in groups)
                    dbCtx.PermissionAudit.Add(
                        Audit(
                            PermissionAuditActionType.Expired,
                            _state.Groups.Groups.GetValueOrDefault(row.GroupEntityId)?.Name
                                ?? $"group:{row.GroupEntityId}",
                            null,
                            null,
                            row.ExpiresAt
                        )
                    );

                foreach (var row in nodes)
                    dbCtx.PermissionAudit.Add(
                        Audit(
                            PermissionAuditActionType.Expired,
                            row.Node,
                            null,
                            PermissionAuditEntries.Format(row.Value),
                            row.ExpiresAt
                        )
                    );

                foreach (var row in meta)
                    dbCtx.PermissionAudit.Add(
                        Audit(
                            PermissionAuditActionType.Expired,
                            row.Key,
                            null,
                            row.Value,
                            row.ExpiresAt
                        )
                    );

                await dbCtx.SaveChangesAsync(ct);

                foreach (var row in groups)
                    _state.MembershipsByGroupId.Remove((row.GroupEntityId, row.IsTemporary));

                foreach (var row in nodes)
                    _state.NodesByNode.Remove((row.Node, row.IsTemporary));

                foreach (var row in meta)
                    _state.MetaByKey.Remove((row.Key, row.IsTemporary));

                _logger.LogInformation(
                    "Expired {Count} permission assignments of player {PlayerId}",
                    groups.Count + nodes.Count + meta.Count,
                    PlayerId
                );
            }

            Resolve();

            // Rows swept straight after an activation may have run out while the grain was
            // collected and its player online, so the client is told even if the projection
            // looks the same as the one this activation began with.
            await PublishChangesAsync(force: groups.Count + nodes.Count + meta.Count > 0, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to expire the permission assignments of player {PlayerId}",
                PlayerId
            );

            ScheduleExpiryIn(TimeSpan.FromMilliseconds(_permissionConfig.ExpiryRetryMs));
        }
    }

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
