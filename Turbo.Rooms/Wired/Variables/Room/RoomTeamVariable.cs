using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

/// <summary>
/// <c>@teams.&lt;colour&gt;.score</c> and <c>.size</c>: one game team's figure, as the official
/// client's Creator Tools list them, red, green, blue then yellow, every score before every size.
/// </summary>
public abstract class RoomTeamVariable(RoomGrain roomGrain) : RoomVariable(roomGrain)
{
    protected abstract GameTeamType Team { get; }

    /// <summary>The figure, <c>score</c> or <c>size</c>.</summary>
    protected abstract string Figure { get; }

    protected override string VariableName =>
        $"@teams.{Team.ToString().ToLowerInvariant()}.{Figure}";
}
