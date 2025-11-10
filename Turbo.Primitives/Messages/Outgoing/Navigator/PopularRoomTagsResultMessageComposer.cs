using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public sealed record PopularRoomTagsResultMessageComposer : IComposer
{
    public object? Data { get; init; }
}
