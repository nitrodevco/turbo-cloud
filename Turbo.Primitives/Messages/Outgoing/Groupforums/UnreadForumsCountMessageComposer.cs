using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Groupforums;

[GenerateSerializer, Immutable]
public sealed record UnreadForumsCountMessageComposer : IComposer
{
    [Id(0)]
    public required int UnreadForumsCount { get; init; }
}
