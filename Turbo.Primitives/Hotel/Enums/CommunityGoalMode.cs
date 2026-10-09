namespace Turbo.Primitives.Hotel.Enums;

/// <summary>
/// How a community goal is played, and so which reception widget shows it: the hotel fills one
/// meter together, or two sides pull a needle each their way.
/// </summary>
public enum CommunityGoalMode
{
    /// <summary>One total, levels reached as it grows (<c>communitygoal</c>).</summary>
    Normal = 0,

    /// <summary>Two sides; the needle leans to the side ahead (<c>communitygoalvsmode</c>).</summary>
    Versus = 1,

    /// <summary>
    /// Two sides, and every player may vote once for one, besides what they buy
    /// (<c>communitygoalvsmodevote</c>).
    /// </summary>
    VersusVote = 2,
}
