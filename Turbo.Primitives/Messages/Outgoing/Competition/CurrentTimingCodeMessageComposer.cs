using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Competition;

[GenerateSerializer, Immutable]
public sealed record CurrentTimingCodeMessageComposer : IComposer
{
    [Id(0)]
    public required string SchedulingStr { get; init; }

    [Id(1)]
    public required string Code { get; init; }
}
