using Orleans;

namespace Turbo.Primitives.Movements;

/// <summary>
/// Why assets move, as the call site that moves them gives it: one <see cref="OperationId"/> per
/// action (a trade, a purchase), its <see cref="Cause"/> code, what caused it by id
/// (<see cref="CauseRef"/>: an offer, a trade, a chest transaction, a command's execution) and
/// who did it (0: the server).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record MovementCause(
    [property: Id(0)] string OperationId,
    [property: Id(1)] string Cause,
    [property: Id(2)] string? CauseRef,
    [property: Id(3)] int ActorPlayerId
);
