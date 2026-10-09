using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Hotel.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Hotel;

/// <summary>
/// The hotel's community goals: the one the reception shows (the last started), what players
/// give it by buying from its catalog pages and by voting, and the editing staff do in the panel.
/// </summary>
public interface ICommunityGoalService
{
    /// <summary>Where the goal shown stands for the player; null when no goal has started.</summary>
    public Task<CommunityGoalProgressSnapshot?> GetProgressAsync(
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>The goal's best contributors, best first; empty for a code there is no goal of.</summary>
    public Task<ImmutableArray<CommunityGoalContributorSnapshot>> GetHallOfFameAsync(
        string code,
        CancellationToken ct
    );

    /// <summary>
    /// A vote for side 1 or 2 of the goal shown, worth one point to it: counted once per player,
    /// and only for a running goal that takes votes. Whether it counted.
    /// </summary>
    public Task<bool> VoteAsync(PlayerId player, int side, CancellationToken ct);

    /// <summary>
    /// Items bought from a catalog page: when the running goal counts that page's purchases, each
    /// is a point to its side.
    /// </summary>
    public Task ContributeAsync(
        PlayerId player,
        int catalogPageId,
        int amount,
        CancellationToken ct
    );

    /// <summary>Every goal, the latest start first.</summary>
    public Task<ImmutableArray<CommunityGoalSnapshot>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Adds a goal (id 0) or changes one. Throws <see cref="System.ArgumentException"/> for a code
    /// that is empty, taken or not a word, an end before the start, levels that aren't one to
    /// three rising scores, prize ranks that don't rise, or a second side on a goal of one.
    /// </summary>
    public Task<CommunityGoalSnapshot> SaveAsync(CommunityGoalSnapshot goal, CancellationToken ct);

    /// <summary>Removes a goal and what was given to it; false when there is none.</summary>
    public Task<bool> DeleteAsync(int id, CancellationToken ct);

    /// <summary>A goal's score so far; null for a goal there isn't.</summary>
    public Task<CommunityGoalStandingSnapshot?> GetStandingAsync(int id, CancellationToken ct);
}
