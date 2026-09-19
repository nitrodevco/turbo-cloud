using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A clothing booth: it holds one look per gender and dresses whoever walks into it. Whoever
/// may edit furniture here dresses the booth from the avatar editor, one gender at a time
/// (<see cref="SetClothingChangeInteraction"/>); the looks live in the legacy data the client
/// reads (<see cref="ClothingChangeData"/>).
/// </summary>
[RoomObjectLogic("clothing_change")]
public class FurnitureClothingChangeLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override async Task OnWalkOnAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOnAsync(ctx, ct);

        if (ctx.RoomObject is not IRoomPlayer player)
            return;

        var (boy, girl) = ClothingChangeData.Parse(GetLegacyString());
        var look = player.Gender == AvatarGenderType.Female ? girl : boy;

        if (look.Length > 0 && look != player.Figure)
            _roomGrain.AvatarModule.ChangePlayerFigure(player.PlayerId, look, player.Gender);
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not SetClothingChangeInteraction dress)
            return false;

        if (!await HasRightsAsync(ctx))
            return Reject(ctx, interaction, "no rights");

        if (!IsFigure(dress.Figure))
            return Reject(ctx, interaction, "not a figure string");

        var (boy, girl) = ClothingChangeData.Parse(GetLegacyString());

        if (dress.Gender == AvatarGenderType.Female)
            girl = dress.Figure;
        else
            boy = dress.Figure;

        await SetLegacyDataAsync(ClothingChangeData.Compose(boy, girl));

        return true;
    }

    /// <summary>
    /// A figure is part codes joined by dots and dashes. Anything else, a comma above all,
    /// would break the two-look data apart, and the text goes out to everyone in the room.
    /// </summary>
    private bool IsFigure(string figure) =>
        figure.Length > 0
        && figure.Length <= _roomGrain._roomConfig.FigureMaxLength
        && figure.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-');
}
