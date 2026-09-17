using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

[GenerateSerializer, Immutable]
public sealed record RoomSettingsSaveErrorEventMessageComposer : IComposer
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required RoomSettingsSaveErrorType Error { get; init; }

    /// <summary>The rejected value, shown by the client next to the field it belongs to.</summary>
    [Id(2)]
    public required string Info { get; init; }
}
