using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Pets;

[GenerateSerializer, Immutable]
public sealed record PetPlacingErrorMessageComposer : IComposer
{
    [Id(0)]
    public required PetPlacingErrorType Error { get; init; }
}
