using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// The flights the projectile addon started, one per furni, so the wired variables can say
/// where a projectile looks to be while the client animates it. A furni keeps its last flight
/// after it lands, which is what lets a stack read how far the shot went; it is dropped when
/// the furni leaves the room.
/// </summary>
public sealed partial class RoomWiredSystem
{
    private readonly Dictionary<RoomObjectId, WiredProjectileFlight> _flightsByObjectId = [];

    internal WiredProjectileFlight? GetProjectileFlight(RoomObjectId objectId) =>
        _flightsByObjectId.GetValueOrDefault(objectId);

    /// <summary>
    /// Remembers a projectile's move as a flight. Whoever stands on the tiles it crosses is
    /// counted now, because that is what the shot was aimed through.
    /// </summary>
    internal void BeginProjectileFlight(
        RoomObjectId objectId,
        int sourceX,
        int sourceY,
        int sourceZ,
        int targetX,
        int targetY,
        int targetZ,
        int durationMs
    )
    {
        var map = _roomGrain.MapModule;
        var path = WiredProjectileFlight.BuildPath(map.ToIdx, sourceX, sourceY, targetX, targetY);

        _flightsByObjectId[objectId] = new WiredProjectileFlight
        {
            Path = path,
            AvatarCountsOnPath =
            [
                .. path.Select(x => _roomGrain.AvatarModule.GetAvatarsOnTile(x).Count()),
            ],
            ItemCountsOnPath =
            [
                .. path.Select(x =>
                    _roomGrain.FurniModule.GetFloorItemsOnTile(x).Count(y => y.ObjectId != objectId)
                ),
            ],
            SourceX = sourceX,
            SourceY = sourceY,
            SourceZ = sourceZ,
            TargetX = targetX,
            TargetY = targetY,
            TargetZ = targetZ,
            StartedAtMs = _roomGrain.NowMs(),
            DurationMs = durationMs,
        };
    }

    private void ForgetProjectileFlight(RoomObjectId objectId) =>
        _flightsByObjectId.Remove(objectId);
}
