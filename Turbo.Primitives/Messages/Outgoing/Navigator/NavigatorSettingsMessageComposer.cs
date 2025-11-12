using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record NavigatorSettingsMessageComposer : IComposer
{
    [Id(0)]
    public int HomeRoomId { get; init; }

    [Id(1)]
    public int RoomIdToEnter { get; init; }
}
