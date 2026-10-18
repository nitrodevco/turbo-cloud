using System;
using Orleans;

namespace Turbo.Primitives.Movements;

/// <summary>
/// One outbox row as a consumer gets it: its <see cref="Id"/> (the cursor position), the
/// movement, and when the row was written. A marker row (<see cref="MovementCauses"/>) moves
/// nothing and says that recording started or stopped.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record OutboxMovement(
    [property: Id(0)] long Id,
    [property: Id(1)] AssetMovement Movement,
    [property: Id(2)] DateTime WrittenAtUtc
);
