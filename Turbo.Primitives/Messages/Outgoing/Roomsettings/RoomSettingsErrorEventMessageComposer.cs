using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

[GenerateSerializer, Immutable]
public sealed record RoomSettingsErrorEventMessageComposer : IComposer
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required RoomSettingsErrorType ErrorCode { get; init; }
}
