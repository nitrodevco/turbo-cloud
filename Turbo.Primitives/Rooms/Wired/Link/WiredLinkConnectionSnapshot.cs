using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Link;

/// <summary>One Link connected to a network, as the "Connected rooms" list shows it (AS3 <c>WiredLinkConnection</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record WiredLinkConnectionSnapshot
{
    [Id(0)]
    public required int RoomId { get; init; }

    [Id(1)]
    public required string RoomName { get; init; }

    [Id(2)]
    public required int FurniId { get; init; }

    [Id(3)]
    public required bool IsBuildersClub { get; init; }

    [Id(4)]
    public required bool ReceiveLocal { get; init; }
}
