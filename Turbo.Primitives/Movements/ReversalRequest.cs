namespace Turbo.Primitives.Movements;

/// <summary>
/// One asset to move back: from where the caller believes it is now (<see cref="Expected"/>) to
/// <see cref="Target"/>. A furni goes inventory to inventory, inventory to nowhere (a dupe taken
/// back) or nowhere to an inventory (made again from <see cref="Recreate"/>, under a new id); a
/// currency goes wallet to nowhere (taken back) or nowhere to a wallet (given back), by
/// <see cref="Amount"/>. Only the owner, kind and place id of a place are compared.
/// </summary>
public sealed record ReversalRequest
{
    public const int MAX_REASON_LENGTH = 255;

    /// <summary>
    /// This reversal's own operation, its idempotency key: asked again, it answers
    /// <see cref="ReversalOutcome.AlreadyApplied"/>. At most
    /// <see cref="AssetMovement.MAX_OPERATION_ID_LENGTH"/> characters.
    /// </summary>
    public required string OperationId { get; init; }

    /// <summary>
    /// The operation it undoes, the cause ref of its movement; at most
    /// <see cref="AssetMovement.MAX_CAUSE_REF_LENGTH"/> characters.
    /// </summary>
    public required string ReversedOperationId { get; init; }

    /// <summary>Whom it is done for, the movement's actor; they must hold <c>items.reverse</c>.</summary>
    public required int ActorPlayerId { get; init; }

    /// <summary>Why, kept on the receipt, not in the movement; at most <see cref="MAX_REASON_LENGTH"/> characters.</summary>
    public required string Reason { get; init; }

    public required AssetRef Asset { get; init; }

    public required AssetPlace Expected { get; init; }

    public required AssetPlace Target { get; init; }

    /// <summary>A currency's amount; 1 for a furni.</summary>
    public long Amount { get; init; } = 1;

    /// <summary>What to make the furni from, when <see cref="Expected"/> is nowhere.</summary>
    public AssetSnapshot? Recreate { get; init; }
}
