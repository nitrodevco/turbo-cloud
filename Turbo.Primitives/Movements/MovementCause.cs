using System;
using System.Globalization;
using Orleans;

namespace Turbo.Primitives.Movements;

/// <summary>
/// Why assets move, as the call site that moves them gives it: one <see cref="OperationId"/> per
/// action (a trade, a purchase), its <see cref="Cause"/> code, what caused it by id
/// (<see cref="CauseRef"/>: an offer, a trade, a chest transaction, a command's execution) and
/// who did it (0: the server). An action that moves assets in more than one call gives each call
/// its own <see cref="FirstSeq"/>, so no two of its movements share an (operation, seq) pair.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record MovementCause(
    [property: Id(0)] string OperationId,
    [property: Id(1)] string Cause,
    [property: Id(2)] string? CauseRef,
    [property: Id(3)] int ActorPlayerId
)
{
    /// <summary>The seq of the first movement written under this cause; the next ones count up from it.</summary>
    [Id(4)]
    public short FirstSeq { get; init; }

    /// <summary>The same action, its movements numbered from <paramref name="firstSeq"/>.</summary>
    public MovementCause StartingAt(int firstSeq) =>
        this with
        {
            FirstSeq = checked((short)firstSeq),
        };

    /// <summary>A new action: its operation id is minted here, unique and time ordered.</summary>
    public static MovementCause New(string cause, int actorPlayerId, string? causeRef = null) =>
        new(Guid.CreateVersion7().ToString("N"), cause, causeRef, actorPlayerId);

    /// <inheritdoc cref="New(string, int, string?)"/>
    public static MovementCause New(string cause, int actorPlayerId, long causeRef) =>
        New(cause, actorPlayerId, causeRef.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// What gives back what this action took after it failed: the same cause and actor, an
    /// operation of its own, and this operation as its cause ref, so the two pair up.
    /// </summary>
    public MovementCause ForRefund() => New(Cause, ActorPlayerId, OperationId);
}
