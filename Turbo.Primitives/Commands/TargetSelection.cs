using System.Collections.Generic;

namespace Turbo.Primitives.Commands;

/// <summary>
/// Who a command's target turned out to be: the players, or the reply to give instead. A
/// selector that matched nobody is a success with no players, not a failure.
/// </summary>
public sealed record TargetSelection(
    IReadOnlyList<ResolvedPlayer> Players,
    bool IsSelector,
    CommandResult? Failure
)
{
    public static TargetSelection Of(ResolvedPlayer player) => new([player], false, null);

    public static TargetSelection Many(IReadOnlyList<ResolvedPlayer> players) =>
        new(players, true, null);

    public static TargetSelection Refused(CommandResult failure) => new([], false, failure);
}
