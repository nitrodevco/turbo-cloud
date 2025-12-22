using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public abstract class WiredDefinition(IRoomObjectContext ctx) : IWiredDefinition
{
    protected readonly IRoomObjectContext _ctx = ctx;

    public IRoomObjectContext Context { get; } = ctx;
}
