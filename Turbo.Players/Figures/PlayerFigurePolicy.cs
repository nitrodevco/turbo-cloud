using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Players.Figures;

/// <summary>
/// <see cref="IPlayerFigurePolicy"/>: the figure data's rules for the player's club level - a
/// member wears at the level the client is told, VIP - and the clothing they own. Nothing is
/// asked of the player before the figure data says there is anything to check.
/// </summary>
internal sealed class PlayerFigurePolicy(
    IFigureDataProvider figureData,
    IPlayerClothingService clothing,
    IGrainFactory grainFactory
) : IPlayerFigurePolicy
{
    public async Task<string> FitAsync(
        PlayerId playerId,
        string figure,
        AvatarGenderType gender,
        CancellationToken ct
    )
    {
        var data = await figureData.GetAsync(ct).ConfigureAwait(false);

        if (
            data.IsEmpty
            || await grainFactory
                .HasPermissionAsync(playerId, PermissionNodes.Figure.ANY, ct)
                .ConfigureAwait(false)
        )
            return figure;

        var clubLevel = await grainFactory.HasActiveClubAsync(playerId, ct).ConfigureAwait(false)
            ? FigureRules.CLUB_LEVEL
            : 0;
        var owned = await clothing.GetOwnedAsync(playerId, ct).ConfigureAwait(false);

        return FigureRules.Fit(data, figure, gender, clubLevel, owned);
    }
}
