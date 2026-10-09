using System;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Hotel.Snapshots;

namespace Turbo.Catalog.Reception;

/// <summary>
/// A community goal's standing put as the client's meter reads it
/// (<c>CommunityGoalWidget.getCurrentNeedleFrame</c> and its versus subclass): the levels
/// reached, the score to the next and how far toward it. A goal of one side reaches levels 0 to 3
/// as its total grows; a versus goal -3 to 3 as one side gets ahead, negative (and its remaining
/// score negative) while side two leads.
/// </summary>
public static class CommunityGoalProgress
{
    /// <summary>Levels the client's meter has room for, either way.</summary>
    public const int MAX_LEVELS = 3;

    public static CommunityGoalProgressSnapshot Compute(
        CommunityGoalSnapshot goal,
        int sideOne,
        int sideTwo,
        DateTime now,
        int personalScore,
        int personalRank
    )
    {
        var levels = goal.LevelScores.Take(MAX_LEVELS).ToImmutableArray();
        var balance = goal.IsVersus ? sideOne - sideTwo : sideOne;
        var direction = balance < 0 ? -1 : 1;
        var score = Math.Abs(balance);
        var reached = levels.Count(x => x <= score);
        int remaining;
        int percent;

        if (reached >= levels.Length)
        {
            remaining = 0;
            percent = 100;
        }
        else
        {
            var from = reached == 0 ? 0 : levels[reached - 1];
            var to = levels[reached];

            remaining = to - score;
            percent = to > from ? (int)((long)(score - from) * 100 / (to - from)) : 0;
        }

        return new CommunityGoalProgressSnapshot
        {
            HasGoalExpired = now >= goal.EndsAt,
            PersonalContributionScore = personalScore,
            PersonalContributionRank = personalRank,
            CommunityTotalScore = sideOne + sideTwo,
            CommunityHighestAchievedLevel = reached * direction,
            ScoreRemainingUntilNextLevel = remaining * direction,
            PercentCompletionTowardsNextLevel = percent,
            GoalCode = goal.Code,
            TimeRemainingInSeconds = (int)
                Math.Clamp(Math.Ceiling((goal.EndsAt - now).TotalSeconds), 0, int.MaxValue),
            RewardUserLimits = goal.RewardRanks,
        };
    }
}
