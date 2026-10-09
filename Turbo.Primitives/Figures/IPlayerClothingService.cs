using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Figures;

/// <summary>
/// The clothing players own: the figure sets the figure data marks as sold, which a player may
/// wear only once they have them. A player online is told what they own when it changes, as the
/// client's avatar editor offers a sold piece only to its owner.
/// </summary>
public interface IPlayerClothingService
{
    public Task<ImmutableHashSet<int>> GetOwnedAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>Gives the player these sets; the number they didn't have yet.</summary>
    public Task<int> GrantAsync(PlayerId playerId, IEnumerable<int> setIds, CancellationToken ct);

    /// <summary>
    /// The class names of the clothing furni the player has bound
    /// (<c>FigureSetIdsMessage.boundFurnitureNames</c>).
    /// </summary>
    public Task<ImmutableArray<string>> GetBoundFurnitureNamesAsync(
        PlayerId playerId,
        CancellationToken ct
    );

    /// <summary>
    /// Binds a clothing furni to the player: its sets are theirs and its definition is among the
    /// bound ones. The player, if online, is told what they own and have bound either way, as the
    /// client puts the clothes on when that answer lists the furni.
    /// </summary>
    public Task BindFurnitureAsync(
        PlayerId playerId,
        int definitionId,
        IEnumerable<int> setIds,
        CancellationToken ct
    );

    /// <summary>Takes these sets from the player; the number they had.</summary>
    public Task<int> RevokeAsync(PlayerId playerId, IEnumerable<int> setIds, CancellationToken ct);

    /// <summary>Tells the player, if online, what they own and the clothing furni they have bound.</summary>
    public Task SendOwnedAsync(PlayerId playerId, CancellationToken ct);
}
