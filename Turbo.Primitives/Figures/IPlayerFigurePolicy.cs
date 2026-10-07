using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Figures;

/// <summary>
/// What a player may wear (<see cref="FigureRules"/>), by their club membership, the clothing
/// they own and whether they may wear anything at all (<c>figure.any</c>).
/// </summary>
public interface IPlayerFigurePolicy
{
    /// <summary>The figure as the player may wear it: the same when they may wear all of it.</summary>
    public Task<string> FitAsync(
        PlayerId playerId,
        string figure,
        AvatarGenderType gender,
        CancellationToken ct
    );
}
