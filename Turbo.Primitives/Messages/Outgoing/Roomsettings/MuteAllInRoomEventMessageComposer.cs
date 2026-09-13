using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

[GenerateSerializer, Immutable]
public sealed record MuteAllInRoomEventMessageComposer : IComposer
{
    [Id(0)]
    public required bool IsMuted { get; init; }
}
