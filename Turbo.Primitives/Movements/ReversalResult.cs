namespace Turbo.Primitives.Movements;

/// <summary>Stored by value on the reversal receipt: never renumber a member.</summary>
public enum ReversalOutcome : byte
{
    /// <summary>Moved now.</summary>
    Applied = 0,

    /// <summary>This operation moved it before; nothing changed now.</summary>
    AlreadyApplied = 1,

    /// <summary>The asset is not where the request expects it (it moved since), or a balance is short.</summary>
    NotWhereExpected = 2,

    /// <summary>The target cannot take it: the same place, a player who does not exist, a definition that does not.</summary>
    TargetInvalid = 3,

    /// <summary>The actor does not hold <c>items.reverse</c>.</summary>
    Forbidden = 4,

    /// <summary>
    /// Not a reversal core makes: a borrowed furni, a pet or a bot, or a furni in or into a room,
    /// a holder or the marketplace.
    /// </summary>
    Unsupported = 5,
}

/// <summary>
/// How a reversal went, and the furni it is about: the request's, or the new id of one made
/// again (also when it was made by an earlier call of the operation); 0 for a currency.
/// </summary>
public sealed record ReversalResult(ReversalOutcome Outcome, long AssetId = 0)
{
    public bool Succeeded => Outcome is ReversalOutcome.Applied or ReversalOutcome.AlreadyApplied;
}
