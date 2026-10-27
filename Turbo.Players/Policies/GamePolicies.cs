using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms.Snapshots;

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
    public const string REGISTRATION = "registration";
    public const string AVATAR_EFFECT = "avatar-effect";
    public const string PURCHASE_FULFILMENT = "purchase-fulfilment";
    public const string PUBLIC_ROOM_ORDER = "public-room-order";
    public const string ROOM_ARRIVAL = "room-arrival";

    /// <summary>No policies: the hotel decides as it always does.</summary>
    public static GamePolicies None => new(NullLogger<GamePolicies>.Instance);

    private readonly Lock _gate = new();
    private ImmutableArray<Entry> _entries = [];

    // The avatar effect policies alone, kept apart since the room asks them on its turn; those
    // that decide what is shown (their own Show) apart again, as the room asks them per viewer.
    private ImmutableArray<Entry> _effects = [];
    private ImmutableArray<Entry> _shows = [];
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
                // Closed, not passed over: a policy there to stop a purchase that fails must not
                // let it through, and money moves on what this returns.
                logger.LogError(
                    ex,
                    "The {Kind} policy {Policy} of {Owner} failed for player {PlayerId}; the purchase is refused",
                    entry.Kind,
                    entry.Policy.GetType().Name,
                    entry.Owner,
                    request.PlayerId.Value
                );

                return PurchaseDecision.Refuse().Clamped();
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

    public async ValueTask<RegistrationDecision> DecideRegistrationAsync(
        RegistrationRequest request,
        CancellationToken ct
    )
    {
        foreach (var entry in Of(REGISTRATION))
        {
            RegistrationDecision decision;

            try
            {
                decision = await ((IRegistrationPolicy)entry.Policy)
                    .DecideAsync(request, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "The {Kind} policy {Policy} of {Owner} failed for new player {PlayerName}; it is passed over",
                    entry.Kind,
                    entry.Policy.GetType().Name,
                    entry.Owner,
                    request.Name
                );

                continue;
            }

            if (!decision.Allowed)
            {
                // The name and the reason, never the address it was asked from.
                logger.LogInformation(
                    "The {Kind} policy {Policy} of {Owner} refused new player {PlayerName} ({Source}): {Reason}",
                    entry.Kind,
                    entry.Policy.GetType().Name,
                    entry.Owner,
                    request.Name,
                    request.Source,
                    decision.Reason
                );

                return decision;
            }
        }

        return RegistrationDecision.Allow;
    }

    public async ValueTask<RoomArrivalDecision> DecideRoomArrivalAsync(
        RoomArrivalRequest request,
        RoomArrivalDecision proposed,
        CancellationToken ct
    )
    {
        var decision = proposed;

        foreach (var entry in Of(ROOM_ARRIVAL))
        {
            try
            {
                decision =
                    await ((IRoomArrivalPolicy)entry.Policy)
                        .DecideAsync(request, decision, ct)
                        .ConfigureAwait(false)
                    ?? decision;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailure(ex, entry, request.PlayerId.Value);
            }
        }

        return decision;
    }

    public bool ShowsAvatarEffects => _shows.Length > 0;

    public int DecideAvatarEffect(AvatarEffectRequest request)
    {
        var effectId = request.EffectId;

        foreach (var entry in _effects)
        {
            try
            {
                effectId = Math.Max(0, ((IAvatarEffectPolicy)entry.Policy).Wear(request, effectId));
            }
            catch (Exception ex)
            {
                LogEffectFailure(ex, entry, request.RoomId.Value);
            }
        }

        return effectId;
    }

    public int ShowAvatarEffect(AvatarEffectView view)
    {
        var effectId = view.EffectId;

        foreach (var entry in _shows)
        {
            try
            {
                effectId = Math.Max(0, ((IAvatarEffectPolicy)entry.Policy).Show(view, effectId));
            }
            catch (Exception ex)
            {
                LogEffectFailure(ex, entry, view.RoomId.Value);
            }
        }

        return effectId;
    }

    public IPurchaseFulfilment? FulfilmentFor(PurchaseRequest request)
    {
        foreach (var entry in Of(PURCHASE_FULFILMENT))
        {
            var fulfilment = (IPurchaseFulfilment)entry.Policy;

            try
            {
                if (fulfilment.Fulfils(request))
                    return fulfilment;
            }
            catch (Exception ex)
            {
                LogFailure(ex, entry, request.PlayerId.Value);
            }
        }

        return null;
    }

    public IReadOnlyList<RoomInfoSnapshot> OrderPublicRooms(
        PublicRoomCategory category,
        IReadOnlyList<RoomInfoSnapshot> rooms
    )
    {
        var order = rooms;

        foreach (var entry in Of(PUBLIC_ROOM_ORDER))
        {
            try
            {
                order = Permutation(
                    order,
                    ((IPublicRoomOrder)entry.Policy).OrderRooms(category, order)
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "The {Kind} policy {Policy} of {Owner} failed for public category {CategoryId}; it is passed over",
                    entry.Kind,
                    entry.Policy.GetType().Name,
                    entry.Owner,
                    category.Id
                );
            }
        }

        return order;
    }

    /// <summary>
    /// Adds <paramref name="owner"/>'s policies (any of the kinds); disposing the result
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
            _effects = [.. _entries.Where(x => x.Kind == AVATAR_EFFECT)];
            _shows = [.. _effects.Where(x => DecidesShown(x.Policy))];
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
                (policy is IRegistrationPolicy, REGISTRATION),
                (policy is IAvatarEffectPolicy, AVATAR_EFFECT),
                (policy is IPurchaseFulfilment, PURCHASE_FULFILMENT),
                (policy is IPublicRoomOrder, PUBLIC_ROOM_ORDER),
                (policy is IRoomArrivalPolicy, ROOM_ARRIVAL),
            }
                .Where(x => x.Is)
                .Select(x => x.Kind),
        ];

    private static int OrderOf(object policy, string kind) =>
        kind switch
        {
            PURCHASE => ((IPurchasePolicy)policy).Order,
            ROOM_ENTRY => ((IRoomEntryPolicy)policy).Order,
            REGISTRATION => ((IRegistrationPolicy)policy).Order,
            AVATAR_EFFECT => ((IAvatarEffectPolicy)policy).Order,
            PURCHASE_FULFILMENT => ((IPurchaseFulfilment)policy).Order,
            PUBLIC_ROOM_ORDER => ((IPublicRoomOrder)policy).Order,
            ROOM_ARRIVAL => ((IRoomArrivalPolicy)policy).Order,
            _ => ((IPlacementPolicy)policy).Order,
        };

    /// <summary>
    /// The rooms of <paramref name="before"/> in the order a policy named them, each once: rooms
    /// it adds are dropped, and rooms it leaves out follow in their order before.
    /// </summary>
    private static IReadOnlyList<RoomInfoSnapshot> Permutation(
        IReadOnlyList<RoomInfoSnapshot> before,
        IReadOnlyList<RoomInfoSnapshot>? named
    )
    {
        var left = before.ToDictionary(x => x.RoomId);
        var order = new List<RoomInfoSnapshot>(before.Count);

        foreach (var room in named ?? [])
            if (room is not null && left.Remove(room.RoomId, out var own))
                order.Add(own);

        order.AddRange(before.Where(x => left.ContainsKey(x.RoomId)));

        return order;
    }

    private void LogFailure(Exception ex, Entry entry, int playerId) =>
        logger.LogError(
            ex,
            "The {Kind} policy {Policy} of {Owner} failed for player {PlayerId}; it is passed over",
            entry.Kind,
            entry.Policy.GetType().Name,
            entry.Owner,
            playerId
        );

    /// <summary>Whether the policy has its own <see cref="IAvatarEffectPolicy.Show"/>, not the interface's, which shows what is worn.</summary>
    private static bool DecidesShown(object policy)
    {
        var map = policy.GetType().GetInterfaceMap(typeof(IAvatarEffectPolicy));
        var show = Array.IndexOf(
            map.InterfaceMethods,
            typeof(IAvatarEffectPolicy).GetMethod(nameof(IAvatarEffectPolicy.Show))
        );

        return map.TargetMethods[show].DeclaringType != typeof(IAvatarEffectPolicy);
    }

    private void LogEffectFailure(Exception ex, Entry entry, int roomId) =>
        logger.LogError(
            ex,
            "The {Kind} policy {Policy} of {Owner} failed in room {RoomId}; it is passed over",
            entry.Kind,
            entry.Policy.GetType().Name,
            entry.Owner,
            roomId
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
            {
                owner._entries = [.. owner._entries.Where(x => !entries.Contains(x))];
                owner._effects = [.. owner._effects.Where(x => !entries.Contains(x))];
                owner._shows = [.. owner._shows.Where(x => !entries.Contains(x))];
            }
        }
    }
}
