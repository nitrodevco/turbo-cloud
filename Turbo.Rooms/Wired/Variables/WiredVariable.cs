using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables;

public abstract class WiredVariable(RoomGrain roomGrain) : IWiredVariable
{
    private readonly RoomGrain _roomGrain = roomGrain;

    public abstract IWiredVariableDefinition Definition { get; }
}
