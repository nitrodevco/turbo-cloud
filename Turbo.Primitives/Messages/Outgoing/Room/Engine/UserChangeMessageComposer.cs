using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record UserChangeMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
