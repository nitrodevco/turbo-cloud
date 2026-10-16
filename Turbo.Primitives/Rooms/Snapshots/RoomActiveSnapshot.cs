using System.Diagnostics.CodeAnalysis;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots;

/// <summary>
/// The live navigator view of an active room, as the room grain last published it. It carries
/// room info only (no password or moderation settings), so it is safe to hand to any caller.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomActiveSnapshot : RoomInfoSnapshot
{
    public RoomActiveSnapshot() { }

    /// <summary>
    /// Starts from the room info, so the fields the two share are mapped once. It used to copy
    /// them one by one and had already stopped copying <c>HiddenByBc</c> when that field moved
    /// up here — the field is not required, so nothing failed to compile, and a room hidden by
    /// Builders Club went on being listed for as long as it was active.
    /// </summary>
    [SetsRequiredMembers]
    public RoomActiveSnapshot(RoomInfoSnapshot info)
        : base(info) { }

    /// <summary>
    /// Players who entered the room within the directory's hot-room window
    /// (<c>RoomConfig.HotRoomWindowSeconds</c>), each counted once.
    /// </summary>
    [Id(0)]
    public int RecentVisitors { get; init; }

    public static RoomActiveSnapshot From(
        RoomInfoSnapshot room,
        int population,
        int recentVisitors = 0
    ) => new(room) { Population = population, RecentVisitors = recentVisitors };
}
