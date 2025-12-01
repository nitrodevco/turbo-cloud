using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record ApproveNameMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
