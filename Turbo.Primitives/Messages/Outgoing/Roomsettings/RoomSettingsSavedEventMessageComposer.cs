using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

[GenerateSerializer, Immutable]
public sealed record RoomSettingsSavedEventMessageComposer : IComposer
{
    [Id(0)]
    public required RoomId RoomId { get; init; }
}
