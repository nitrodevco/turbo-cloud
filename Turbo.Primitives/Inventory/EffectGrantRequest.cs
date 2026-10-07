using Orleans;

namespace Turbo.Primitives.Inventory;

/// <summary>Copies of one effect that a purchase or a grant would give.</summary>
[GenerateSerializer, Immutable]
public sealed record EffectGrantRequest
{
    [Id(0)]
    public required int EffectId { get; init; }

    /// <summary>
    /// A client can name any quantity, so callers clamp to <see cref="int.MaxValue"/>; the grain
    /// adds in a wider type and refuses anything past its cap.
    /// </summary>
    [Id(1)]
    public required int Copies { get; init; }
}
