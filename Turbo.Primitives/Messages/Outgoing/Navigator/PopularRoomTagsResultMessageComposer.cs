using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record PopularRoomTagsResultMessageComposer : IComposer
{
    public object? Data { get; init; }
}
