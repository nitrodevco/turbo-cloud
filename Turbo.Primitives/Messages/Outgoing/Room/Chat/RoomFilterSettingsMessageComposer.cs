using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Chat;

[GenerateSerializer, Immutable]
public sealed record RoomFilterSettingsMessageComposer : IComposer
{
    [Id(0)]
    public required List<string> BadWords { get; init; }
}
