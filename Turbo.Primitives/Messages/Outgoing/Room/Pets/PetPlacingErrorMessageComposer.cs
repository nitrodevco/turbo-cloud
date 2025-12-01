using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Pets;

[GenerateSerializer, Immutable]
public sealed record PetPlacingErrorMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
