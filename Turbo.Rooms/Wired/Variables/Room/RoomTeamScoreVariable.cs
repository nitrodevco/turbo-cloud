using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

/// <summary>
/// <c>@teams.&lt;colour&gt;.score</c>: the team's game score, whoever is on it. Writing it sets
/// the score, as the official client lets it be written.
/// </summary>
public abstract class RoomTeamScoreVariable(RoomGrain roomGrain) : RoomTeamVariable(roomGrain)
{
    protected override string Figure => "score";

    // After @wired_timer (36): red 34 down to yellow 31.
    protected override ushort Order => (ushort)(35 - (int)Team);

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.AlwaysAvailable
        | WiredVariableFlags.CanWriteValue;

    protected override WiredVariableValue GetValueForRoom(RoomGrain roomGrain) =>
        WiredVariableValue.Parse(GameSystem.GetScore(Team));

    public override async Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    ) =>
        CanBind(key)
        && await GameSystem.SetScoreAsync(Team, value.ClampToInt(), CancellationToken.None);
}

public sealed class RoomTeamRedScoreVariable(RoomGrain roomGrain) : RoomTeamScoreVariable(roomGrain)
{
    protected override GameTeamType Team => GameTeamType.Red;
}

public sealed class RoomTeamGreenScoreVariable(RoomGrain roomGrain)
    : RoomTeamScoreVariable(roomGrain)
{
    protected override GameTeamType Team => GameTeamType.Green;
}

public sealed class RoomTeamBlueScoreVariable(RoomGrain roomGrain)
    : RoomTeamScoreVariable(roomGrain)
{
    protected override GameTeamType Team => GameTeamType.Blue;
}

public sealed class RoomTeamYellowScoreVariable(RoomGrain roomGrain)
    : RoomTeamScoreVariable(roomGrain)
{
    protected override GameTeamType Team => GameTeamType.Yellow;
}
