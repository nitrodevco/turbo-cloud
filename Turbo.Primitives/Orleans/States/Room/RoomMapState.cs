using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Orleans.States.Room;

public sealed class RoomMapState
{
    public required double[] TileHeights { get; set; }
    public required short[] TileRelativeHeights { get; set; }
    public required byte[] TileStates { get; set; }
    public required long[] TileHighestFloorItems { get; set; }
    public required List<long>[] TileFloorStacks { get; set; }
}
