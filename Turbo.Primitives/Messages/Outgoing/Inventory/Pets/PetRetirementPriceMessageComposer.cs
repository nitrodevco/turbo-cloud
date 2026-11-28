using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Pets;

/// <summary>
/// What retiring a pet costs. The client shows it in its confirmation as "n credits" when it
/// is above zero.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PetRetirementPriceMessageComposer : IComposer
{
    [Id(0)]
    public required int Credits { get; init; }
}
