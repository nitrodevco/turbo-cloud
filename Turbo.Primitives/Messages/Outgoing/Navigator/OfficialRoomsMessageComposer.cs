using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record OfficialRoomsMessageComposer : IComposer
{
    public object? PromotedRooms { get; init; }
    public object? Data { get; init; }
    public object? AdRoom { get; init; }
}
