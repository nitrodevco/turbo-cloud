using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record GuestRoomSearchResultMessageComposer : IComposer
{
    public object? Data { get; init; }
}
