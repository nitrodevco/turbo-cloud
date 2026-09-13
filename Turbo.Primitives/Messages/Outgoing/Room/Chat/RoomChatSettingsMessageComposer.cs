using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Chat;

[GenerateSerializer, Immutable]
public sealed record RoomChatSettingsMessageComposer : IComposer
{
    [Id(0)]
    public required ChatFloodSensitivityType ChatProtection { get; init; }
}
