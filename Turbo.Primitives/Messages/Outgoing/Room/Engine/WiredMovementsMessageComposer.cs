using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record WiredMovementsMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
