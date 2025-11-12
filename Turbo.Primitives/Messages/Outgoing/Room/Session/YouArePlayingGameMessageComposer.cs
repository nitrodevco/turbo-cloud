using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

[GenerateSerializer, Immutable]
public sealed record YouArePlayingGameMessageComposer : IComposer
{
    [Id(0)]
    public required bool IsPlaying { get; init; }
}
