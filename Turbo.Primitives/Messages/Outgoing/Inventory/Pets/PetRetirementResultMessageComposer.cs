using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Pets;

/// <summary>The answer to <c>RetirePet</c>; the client drops the pet from its list on success.</summary>
[GenerateSerializer, Immutable]
public sealed record PetRetirementResultMessageComposer : IComposer
{
    [Id(0)]
    public required int PetId { get; init; }

    [Id(1)]
    public required bool Success { get; init; }
}
