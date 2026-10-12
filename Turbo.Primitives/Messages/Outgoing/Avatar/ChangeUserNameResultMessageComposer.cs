using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Avatar;

[GenerateSerializer, Immutable]
public sealed record ChangeUserNameResultMessageComposer : IComposer
{
    [Id(0)]
    public required NameCheckResult Result { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public ImmutableArray<string> Suggestions { get; init; } = [];
}
