using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// One block of a chatlog (<c>ChatlogCtrl.populateEvidence</c>): its type - 1 a room, which
/// carries <c>roomId</c> and <c>roomName</c> - and its lines, oldest first.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ModChatlogRecordSnapshot
{
    public const int ROOM = 1;

    [Id(0)]
    public required int RecordType { get; init; }

    [Id(1)]
    public required int RoomId { get; init; }

    [Id(2)]
    public required string RoomName { get; init; }

    [Id(3)]
    public required ImmutableArray<ModChatlogLineSnapshot> Lines { get; init; }
}
