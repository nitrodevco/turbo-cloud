using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

/// <summary><c>@teams.&lt;colour&gt;.size</c>: how many players are on the team. Read only.</summary>
public abstract class RoomTeamSizeVariable(RoomGrain roomGrain) : RoomTeamVariable(roomGrain)
{
    protected override string Figure => "size";

    // After the scores: red 30 down to yellow 27.
    protected override ushort Order => (ushort)(31 - (int)Team);

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForRoom(RoomGrain roomGrain) =>
        WiredVariableValue.Parse(GameSystem.GetTeamMembers(Team).Count());
}

public sealed class RoomTeamRedSizeVariable(RoomGrain roomGrain) : RoomTeamSizeVariable(roomGrain)
{
    protected override GameTeamType Team => GameTeamType.Red;
}

public sealed class RoomTeamGreenSizeVariable(RoomGrain roomGrain) : RoomTeamSizeVariable(roomGrain)
{
    protected override GameTeamType Team => GameTeamType.Green;
}

public sealed class RoomTeamBlueSizeVariable(RoomGrain roomGrain) : RoomTeamSizeVariable(roomGrain)
{
    protected override GameTeamType Team => GameTeamType.Blue;
}

public sealed class RoomTeamYellowSizeVariable(RoomGrain roomGrain)
    : RoomTeamSizeVariable(roomGrain)
{
    protected override GameTeamType Team => GameTeamType.Yellow;
}
