using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record CanCreateRoomEventMessageComposer : IComposer
{
    public bool CanCreateEvent { get; init; }
    public int ErrorCode { get; init; }
}
