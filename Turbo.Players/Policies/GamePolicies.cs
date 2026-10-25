using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Primitives.Policies;

namespace Turbo.Players.Policies;

/// <summary>
/// The registered game policies (<see cref="IGamePolicies"/>), each with the plugin that
/// registered it, run in order (<c>Order</c>, then plugin key, then registration).
/// </summary>
public sealed class GamePolicies(ILogger<GamePolicies> logger) : IGamePolicies
{
    public const string PURCHASE = "purchase";
    public const string ROOM_ENTRY = "room-entry";
    public const string PLACEMENT = "placement";

    /// <summary>No policies: the hotel decides as it always does.</summary>
    public static GamePolicies None => new(NullLogger<GamePolicies>.Instance);

    private readonly Lock _gate = new();
    private ImmutableArray<Entry> _entries = [];
    private long _sequence;

    public ImmutableArray<RegisteredPolicy> Registered
    {
        get
        {
            var entries = _entries;

            return
            [
                .. entries.Select(x => new RegisteredPolicy(
                    x.Kind,
                    x.Owner,
                    x.Policy.GetType().Name,
                    x.Order,
                    [
                        .. entries
                            .Where(y =>
                                y.Kind == x.Kind
                                && !string.Equals(
                                    y.Owner,
                                    x.Owner,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            .Select(y => y.Owner)
                            .Distinct(StringComparer.OrdinalIgnoreCase),
                    ]
                )),
            ];
        }
    }

    public async ValueTask<PurchaseDecision> DecidePurchaseAsync(
        PurchaseRequest request,
        PurchaseDecision proposed,
        CancellationToken ct
    )
    {
        var decision = proposed;

        foreach (var entry in Of(PURCHASE))
        {
            PurchaseDecision next;

            try
            {
                next = await ((IPurchasePolicy)entry.Policy)
                    .DecideAsync(request, decision, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailure(ex, entry, request.PlayerId.Value);

                continue;
            }

            decision = next.Clamped();

            if (!decision.Allowed)
            {
                LogRefusal(entry, request.PlayerId.Value, decision.Reason);

                return decision;
            }
        }

        return decision;
    }

    public async ValueTask<RoomEntryDecision> DecideRoomEntryAsync(
        RoomEntryRequest request,
        CancellationToken ct
    )
    {
        foreach (var entry in Of(ROOM_ENTRY))
        {
            RoomEntryDecision decision;

            try
            {
                decision = await ((IRoomEntryPolicy)entry.Policy)
                    .DecideAsync(request, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailure(ex, entry, request.PlayerId.Value);

                continue;
            }

            if (!decision.Allowed)
            {
                LogRefusal(entry, request.PlayerId.Value, decision.Reason);

                return decision;
            }
        }

        return RoomEntryDecision.Allow;
    }

    public async ValueTask<PlacementDecision> DecidePlacementAsync(
        PlacementRequest request,
        CancellationToken ct
    )
    {
        foreach (var entry in Of(PLACEMENT))
        {
            PlacementDecision decision;

            try
            {
                decision = await ((IPlacementPolicy)entry.Policy)
                    .DecideAsync(request, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailure(ex, entry, request.PlayerId.Value);

                continue;
            }

            if (!decision.Allowed)
            {
                LogRefusal(entry, request.PlayerId.Value, decision.Reason);

                return decision;
            }
        }

        return PlacementDecision.Allow;
    }

    /// <summary>
    /// Adds <paramref name="owner"/>'s policies (any of the three kinds); disposing the result
    /// takes them away. A kind another owner already has a policy of is logged as a conflict.
    /// </summary>
    public IDisposable Register(string owner, params object[] policies)
    {
        Entry[] added;
        ImmutableArray<Entry> all;

        lock (_gate)
        {
            added =
            [
                .. policies.SelectMany(policy =>
                    KindsOf(policy)
                        .Select(kind => new Entry(
                            kind,
                            owner,
                            policy,
                            OrderOf(policy, kind),
                            ++_sequence
                        ))
                ),
            ];
            _entries =
            [
                .. _entries
                    .Concat(added)
                    .OrderBy(x => x.Kind, StringComparer.Ordinal)
                    .ThenBy(x => x.Order)
                    .ThenBy(x => x.Owner, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Sequence),
            ];
            all = _entries;
        }

        foreach (var kind in added.Select(x => x.Kind).Distinct())
        {
            var owners = all.Where(x => x.Kind == kind)
                .Select(x => x.Owner)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (owners.Count > 1)
                logger.LogWarning(
                    "Conflict: {Owners} each have a {Kind} policy; they all run, in this order, each on what the one before decided",
                    string.Join(", ", owners),
                    kind
                );
        }

        return new Removal(this, added);
    }

    private ImmutableArray<Entry> Of(string kind)
    {
        var entries = _entries;

        return entries.IsEmpty ? entries : [.. entries.Where(x => x.Kind == kind)];
    }

    private static string[] KindsOf(object policy) =>
        [
            .. new (bool Is, string Kind)[]
            {
                (policy is IPurchasePolicy, PURCHASE),
                (policy is IRoomEntryPolicy, ROOM_ENTRY),
                (policy is IPlacementPolicy, PLACEMENT),
            }
                .Where(x => x.Is)
                .Select(x => x.Kind),
        ];

    private static int OrderOf(object policy, string kind) =>
        kind switch
        {
            PURCHASE => ((IPurchasePolicy)policy).Order,
            ROOM_ENTRY => ((IRoomEntryPolicy)policy).Order,
            _ => ((IPlacementPolicy)policy).Order,
        };

    private void LogFailure(Exception ex, Entry entry, int playerId) =>
        logger.LogError(
            ex,
            "The {Kind} policy {Policy} of {Owner} failed for player {PlayerId}; it is passed over",
            entry.Kind,
            entry.Policy.GetType().Name,
            entry.Owner,
            playerId
        );

    private void LogRefusal(Entry entry, int playerId, string? reason) =>
        logger.LogInformation(
            "The {Kind} policy {Policy} of {Owner} refused player {PlayerId}: {Reason}",
            entry.Kind,
            entry.Policy.GetType().Name,
            entry.Owner,
            playerId,
            reason
        );

    private sealed record Entry(string Kind, string Owner, object Policy, int Order, long Sequence);

    private sealed class Removal(GamePolicies owner, Entry[] entries) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._gate)
                owner._entries = [.. owner._entries.Where(x => !entries.Contains(x))];
        }
    }
}
