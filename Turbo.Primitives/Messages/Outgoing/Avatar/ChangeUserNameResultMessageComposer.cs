using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Avatar;

[GenerateSerializer, Immutable]
public sealed record ChangeUserNameResultMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
