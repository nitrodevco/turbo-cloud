using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

[GenerateSerializer, Immutable]
public sealed record ModeratorCautionEventMessageComposer : IComposer
{
    [Id(0)]
    public required string Message { get; init; }

    [Id(1)]
    public required string Url { get; init; }
}
