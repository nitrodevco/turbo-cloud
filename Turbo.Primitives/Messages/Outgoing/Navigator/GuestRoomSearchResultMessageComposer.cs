using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record GuestRoomSearchResultMessageComposer : IComposer
{
    [Id(0)]
    public object? Data { get; init; }
}
