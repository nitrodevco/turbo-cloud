using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

[GenerateSerializer, Immutable]
public sealed record CantConnectMessageComposer : IComposer
{
    [Id(0)]
    public required RoomConnectionErrorType ErrorType { get; init; }

    [Id(1)]
    public string? AdditionalInfo { get; init; }
}
