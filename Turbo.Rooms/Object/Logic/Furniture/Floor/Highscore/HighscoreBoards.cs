using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Highscore;

/// <summary>
/// The wired highscore boards and how a game's result is written to one. The kinds and their
/// numbers are Flash's <c>HighScoreDisplayWidget</c>: score type 0 best teams (one row per team,
/// its best score), 1 most wins, 2 best scores (a new row every game), 3 fastest and 4 longest
/// time; clear type 0 all time, 1 daily, 2 weekly, 3 monthly. Sulake's furni data names them
/// <c>highscore_&lt;kind&gt;*&lt;clear type + 1&gt;</c>.
/// </summary>
public static class HighscoreBoards
{
    public const string LOGIC_NAME = "highscore";

    public const int PER_TEAM = 0;
    public const int MOST_WINS = 1;
    public const int CLASSIC = 2;
    public const int FASTEST_TIME = 3;
    public const int LONGEST_TIME = 4;

    public const int CLEAR_NEVER = 0;
    public const int CLEAR_DAILY = 1;
    public const int CLEAR_WEEKLY = 2;
    public const int CLEAR_MONTHLY = 3;

    /// <summary>The rows a board keeps.</summary>
    public const int MAX_ENTRIES = 50;

    private static readonly Dictionary<string, int> SCORE_TYPE_BY_KIND = new(StringComparer.Ordinal)
    {
        ["highscore_perteam"] = PER_TEAM,
        ["highscore_mostwin"] = MOST_WINS,
        ["highscore_classic"] = CLASSIC,
        ["highscore_fastesttime"] = FASTEST_TIME,
        ["highscore_longesttime"] = LONGEST_TIME,
    };

    /// <summary>The kind and clear type a classname names, such as <c>highscore_perteam*2</c>.</summary>
    public static bool TryParse(string classname, out int scoreType, out int clearType)
    {
        clearType = CLEAR_NEVER;

        var star = classname.IndexOf('*');
        var kind = star < 0 ? classname : classname[..star];

        if (!SCORE_TYPE_BY_KIND.TryGetValue(kind, out scoreType))
        {
            scoreType = -1;

            return false;
        }

        if (
            star >= 0
            && int.TryParse(
                classname.AsSpan(star + 1),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var variant
            )
        )
            clearType = Math.Clamp(variant - 1, CLEAR_NEVER, CLEAR_MONTHLY);

        return true;
    }

    /// <summary>When the period a board of this clear type is in at <paramref name="now"/> began.</summary>
    public static long PeriodStart(int clearType, DateTimeOffset now)
    {
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

        return clearType switch
        {
            CLEAR_DAILY => day.ToUnixTimeSeconds(),
            CLEAR_WEEKLY => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)).ToUnixTimeSeconds(),
            CLEAR_MONTHLY => new DateTimeOffset(
                day.Year,
                day.Month,
                1,
                0,
                0,
                0,
                TimeSpan.Zero
            ).ToUnixTimeSeconds(),
            _ => 0,
        };
    }

    /// <summary>
    /// The rows after a game: <paramref name="teams"/> are the teams that played, each with its
    /// score and players; the winners are those with the best score above zero. A row is a set of
    /// players, so the same players on another team colour share it.
    /// </summary>
    public static List<HighscoreEntry> Record(
        int scoreType,
        IReadOnlyList<HighscoreEntry> rows,
        IReadOnlyList<(int Score, IReadOnlyList<string> Users)> teams,
        int seconds
    )
    {
        var result = rows.Select(Copy).ToList();
        var played = teams
            .Where(team => team.Users.Count > 0)
            .Select(team => (team.Score, Users: team.Users.Order(StringComparer.Ordinal).ToList()))
            .ToList();

        if (played.Count == 0)
            return result;

        var best = played.Max(team => team.Score);
        var winners = played.Where(team => best > 0 && team.Score == best).ToList();

        switch (scoreType)
        {
            case PER_TEAM:
                foreach (var (score, users) in played.Where(team => team.Score > 0))
                {
                    if (Find(result, users) is { } row)
                        row.Score = Math.Max(row.Score, score);
                    else
                        result.Add(new HighscoreEntry { Score = score, Users = users });
                }

                break;
            case MOST_WINS:
                foreach (var (_, users) in winners)
                {
                    if (Find(result, users) is { } row)
                        row.Score++;
                    else
                        result.Add(new HighscoreEntry { Score = 1, Users = users });
                }

                break;
            case CLASSIC:
                foreach (var (score, users) in played.Where(team => team.Score > 0))
                    result.Add(new HighscoreEntry { Score = score, Users = users });

                break;
            case FASTEST_TIME:
            case LONGEST_TIME:
                foreach (var (_, users) in winners)
                {
                    if (Find(result, users) is not { } row)
                        result.Add(new HighscoreEntry { Score = seconds, Users = users });
                    else if (scoreType == FASTEST_TIME)
                        row.Score = Math.Min(row.Score, seconds);
                    else
                        row.Score = Math.Max(row.Score, seconds);
                }

                break;
        }

        var ordered =
            scoreType == FASTEST_TIME
                ? result.OrderBy(row => row.Score)
                : result.OrderByDescending(row => row.Score);

        return [.. ordered.Take(MAX_ENTRIES)];
    }

    private static HighscoreEntry? Find(List<HighscoreEntry> rows, List<string> users) =>
        rows.FirstOrDefault(row =>
            row.Users.Order(StringComparer.Ordinal).SequenceEqual(users, StringComparer.Ordinal)
        );

    private static HighscoreEntry Copy(HighscoreEntry row) =>
        new() { Score = row.Score, Users = [.. row.Users] };
}
