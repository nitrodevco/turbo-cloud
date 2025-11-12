using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record DoorbellMessageComposer : IComposer
{
    [Id(0)]
    public string? Username { get; init; }
}
