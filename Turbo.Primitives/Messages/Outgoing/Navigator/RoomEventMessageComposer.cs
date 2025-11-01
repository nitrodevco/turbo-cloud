using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record RoomEventMessageComposer : IComposer
{
    public object? Data { get; init; }
}
