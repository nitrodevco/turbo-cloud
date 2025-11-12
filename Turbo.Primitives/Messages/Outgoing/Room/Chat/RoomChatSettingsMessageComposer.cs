using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Chat;

[GenerateSerializer, Immutable]
public sealed record RoomChatSettingsMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
