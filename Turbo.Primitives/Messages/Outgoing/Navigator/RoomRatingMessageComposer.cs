using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record RoomRatingMessageComposer : IComposer
{
    [Id(0)]
    public int Rating { get; init; }

    [Id(1)]
    public bool CanRate { get; init; }
}
