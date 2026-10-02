using Orleans;

namespace Turbo.Primitives.Pets.Snapshots;

[GenerateSerializer, Immutable]
public sealed record PetNutritionOperationResult
{
    [Id(0)]
    public required int Nutrition { get; init; }

    [Id(1)]
    public required int ActualGain { get; init; }
}
