using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Groupforums;

[GenerateSerializer, Immutable]
public sealed record UnreadForumsCountMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
