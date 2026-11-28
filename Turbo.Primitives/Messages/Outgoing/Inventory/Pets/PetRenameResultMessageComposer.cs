using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Pets;

/// <summary>
/// The answer to <c>RenamePet</c>. On success the client asks for the pet's info and its pets
/// tab again; otherwise it says the name could not be saved.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PetRenameResultMessageComposer : IComposer
{
    [Id(0)]
    public required int PetId { get; init; }

    [Id(1)]
    public required bool Success { get; init; }
}
