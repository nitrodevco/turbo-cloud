using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Permissions;

[GenerateSerializer, Immutable]
public sealed record YouAreNotControllerMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
