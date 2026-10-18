using System;
using Orleans;

namespace Turbo.Primitives.Movements;

/// <summary>
/// One asset changing owner, place or existence; a move to another tile of the same room is not
/// one. A consumer orders one asset's movements by <see cref="OccurredAtUtc"/>, then
/// <see cref="OperationId"/> and <see cref="Seq"/>, and drops a repeated (operation, seq) pair: a
/// movement may be delivered more than once, and the outbox holds them in the order they were
/// written, not the order they happened. See <c>docs/movements.md</c>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetMovement
{
    public const int MAX_OPERATION_ID_LENGTH = 160;
    public const int MAX_CAUSE_LENGTH = 48;
    public const int MAX_CAUSE_REF_LENGTH = 64;

    /// <summary>The action this is part of; at most <see cref="MAX_OPERATION_ID_LENGTH"/> characters.</summary>
    [Id(0)]
    public required string OperationId { get; init; }

    /// <summary>Its place in the action, from 0: a trade has one per item on each side.</summary>
    [Id(1)]
    public required short Seq { get; init; }

    /// <summary>UTC; when the action happened, which may be before its row is written.</summary>
    [Id(2)]
    public required DateTime OccurredAtUtc { get; init; }

    [Id(3)]
    public required AssetRef Asset { get; init; }

    [Id(4)]
    public required AssetPlace From { get; init; }

    [Id(5)]
    public required AssetPlace To { get; init; }

    /// <summary>A currency's amount, always positive; 1 for a furni, pet or bot.</summary>
    [Id(6)]
    public long Amount { get; init; } = 1;

    /// <summary>Who did it, who may own neither place; 0 for the server.</summary>
    [Id(7)]
    public int ActorPlayerId { get; init; }

    /// <summary>A stable code for why, such as <c>catalog.purchase</c>; at most <see cref="MAX_CAUSE_LENGTH"/> characters.</summary>
    [Id(8)]
    public required string Cause { get; init; }

    /// <summary>What caused it, by id: an offer, a trade, a chest transaction, a command's execution.</summary>
    [Id(9)]
    public string? CauseRef { get; init; }

    /// <summary>On a movement that creates or destroys a furni only: what it would take to make it again.</summary>
    [Id(10)]
    public AssetSnapshot? Snapshot { get; init; }

    /// <summary>A movement with the operation, cause and actor of <paramref name="cause"/>.</summary>
    public static AssetMovement For(
        MovementCause cause,
        short seq,
        DateTime occurredAtUtc,
        AssetRef asset,
        AssetPlace from,
        AssetPlace to,
        long amount = 1,
        AssetSnapshot? snapshot = null
    ) =>
        new()
        {
            OperationId = cause.OperationId,
            Seq = seq,
            OccurredAtUtc = occurredAtUtc,
            Asset = asset,
            From = from,
            To = to,
            Amount = amount,
            ActorPlayerId = cause.ActorPlayerId,
            Cause = cause.Cause,
            CauseRef = cause.CauseRef,
            Snapshot = snapshot,
        };
}
