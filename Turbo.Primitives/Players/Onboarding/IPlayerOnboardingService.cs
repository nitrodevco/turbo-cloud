using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Players.Onboarding;

/// <summary>A name check's answer: the result code and, for a taken name, names that are free.</summary>
public sealed record NameCheck(
    NameCheckResult Result,
    string Name,
    ImmutableArray<string> Suggestions
);

/// <summary>
/// The new user flow (<c>Turbo:Onboarding</c>): which of its steps a player still has to do, and
/// the name step's checks. The starter room itself is the navigator's.
/// </summary>
public interface IPlayerOnboardingService
{
    /// <summary>Login action 0: the look and name step.</summary>
    public const short CHOOSE_NAME = 0;

    /// <summary>Login action 1: the starter room step.</summary>
    public const short PICK_ROOM = 1;

    /// <summary>
    /// <c>AuthenticationOK</c>'s suggested login actions: the steps the player still has to do,
    /// none when the flow is off.
    /// </summary>
    public Task<ImmutableArray<short>> GetLoginActionsAsync(
        PlayerId playerId,
        CancellationToken ct
    );

    /// <summary>Whether <paramref name="name"/> could be the player's; nothing is changed.</summary>
    public Task<NameCheck> CheckNameAsync(PlayerId playerId, string name, CancellationToken ct);

    /// <summary>Checks the name again and makes it the player's, ending the name step.</summary>
    public Task<NameCheck> ChooseNameAsync(PlayerId playerId, string name, CancellationToken ct);

    /// <summary>Whether the player still has the starter room step to do (and the flow is on).</summary>
    public Task<bool> IsRoomPendingAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>Ends the starter room step; false when it was already done.</summary>
    public Task<bool> CompleteRoomAsync(PlayerId playerId, CancellationToken ct);
}
