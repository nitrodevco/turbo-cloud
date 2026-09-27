using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Competition;

[GenerateSerializer, Immutable]
public sealed record SecondsUntilMessageComposer : IComposer
{
    [Id(0)]
    public required string TimeStr { get; init; }

    [Id(1)]
    public required int SecondsUntil { get; init; }
}
