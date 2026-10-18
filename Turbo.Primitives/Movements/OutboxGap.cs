using Orleans;

namespace Turbo.Primitives.Movements;

/// <summary>
/// Outbox ids a consumer will never be given because they were pruned
/// (<c>Turbo:Movements:OutboxRetentionHours</c>) before it read them, first to last inclusive.
/// An id in the range may never have held a row: a save that rolled back still used its ids.
/// </summary>
[GenerateSerializer, Immutable]
public readonly record struct OutboxGap(
    [property: Id(0)] long FirstId,
    [property: Id(1)] long LastId
);
