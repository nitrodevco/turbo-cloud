using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public abstract class WiredDefinition : IWiredDefinition
{
    protected readonly IRoomFloorItemContext _ctx;

    public IRoomFloorItemContext Context => _ctx;

    public WiredDefinition(IRoomFloorItemContext ctx)
    {
        _ctx = ctx;
    }
}
