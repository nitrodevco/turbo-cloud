using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record RoomRatingMessageComposer : IComposer
{
    public int Rating { get; init; }
    public bool CanRate { get; init; }
}
