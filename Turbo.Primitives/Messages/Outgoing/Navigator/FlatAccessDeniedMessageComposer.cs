using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record FlatAccessDeniedMessageComposer : IComposer
{
    public int RoomId { get; init; }
    public string? Username { get; init; }
}
