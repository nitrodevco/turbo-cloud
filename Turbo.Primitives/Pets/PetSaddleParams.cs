using System.Collections.Immutable;

namespace Turbo.Primitives.Pets;

/// <summary>
/// What a saddle does, from its definition's <c>customparams</c>
/// (<see cref="PetProductParams.ParseSaddle"/>).
/// </summary>
/// <param name="PetTypeId">The pet type it fits.</param>
/// <param name="CustomParts">The parts it puts on the pet: flat triples of layer, part, palette.</param>
/// <param name="RiderEffectId">The effect a rider of the saddled pet wears.</param>
public sealed record PetSaddleParams(
    int PetTypeId,
    ImmutableArray<int> CustomParts,
    int RiderEffectId
);
