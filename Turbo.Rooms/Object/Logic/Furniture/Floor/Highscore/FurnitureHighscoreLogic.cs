using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Highscore;

/// <summary>
/// A wired highscore board. When a game ends the room hands every board the teams that played,
/// and the board writes them down by its kind (<see cref="HighscoreBoards"/>); the Wired Faculty
/// tutorial "Add a specific score to a leaderboard" (12/03/2025): "The leaderboard gets updated
/// every time a counter ends in the room". A daily, weekly or monthly board starts empty in each
/// new period. Switching it on and off is the plain toggle; the client shows the rows while it
/// is on.
/// </summary>
[RoomObjectLogic(HighscoreBoards.LOGIC_NAME)]
public class FurnitureHighscoreLogic : FurnitureFloorLogic
{
    protected override StuffDataType _stuffDataType => StuffDataType.HighscoreKey;

    private readonly int _scoreType;
    private readonly int _clearType;

    public FurnitureHighscoreLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
        : base(stuffDataFactory, ctx)
    {
        if (!HighscoreBoards.TryParse(ctx.Definition.Name, out _scoreType, out _clearType))
            (_scoreType, _clearType) = (HighscoreBoards.CLASSIC, HighscoreBoards.CLEAR_NEVER);

        if (StuffData is IHighscoreStuffData board)
        {
            board.SetTypes(_scoreType, _clearType);
            ClearIfStale(board, DateTimeOffset.UtcNow);
        }
    }

    public IReadOnlyList<HighscoreEntry> Entries =>
        StuffData is IHighscoreStuffData board ? board.Entries : [];

    /// <summary>Writes a finished game to the board and shows the room the new rows.</summary>
    public async Task RecordGameAsync(
        IReadOnlyList<(int Score, IReadOnlyList<string> Users)> teams,
        int seconds,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        if (StuffData is not IHighscoreStuffData board)
            return;

        ClearIfStale(board, now);

        board.SetEntries(
            HighscoreBoards.Record(_scoreType, board.Entries, teams, seconds),
            HighscoreBoards.PeriodStart(_clearType, now)
        );

        PersistStuffData(true);

        await OnStateChangedAsync(ct);
    }

    private void ClearIfStale(IHighscoreStuffData board, DateTimeOffset now)
    {
        var period = HighscoreBoards.PeriodStart(_clearType, now);

        if (board.Entries.Count > 0 && board.PeriodStartedAt != period)
            board.SetEntries([], period);
    }
}
