using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Wall;

/// <summary>
/// A post-it wall. Anyone may double-click it: the server asks their client to open the note
/// editor at the wall's location, and the note comes back as <see cref="AddSpamWallPostItInteraction"/>,
/// which creates a post-it owned by the wall's owner.
/// </summary>
[RoomObjectLogic("spam_wall")]
public class FurnitureSpamWallLogic(
    IStuffDataFactory stuffDataFactory,
    IFurnitureDefinitionProvider definitionProvider,
    IRoomWallItemContext ctx
) : FurnitureWallLogic(stuffDataFactory, ctx)
{
    private readonly IFurnitureDefinitionProvider _definitionProvider = definitionProvider;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Everybody;

    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        SendToPlayerAsync(
            ctx.PlayerId,
            new RequestSpamWallPostItMessageComposer
            {
                ItemId = _ctx.ObjectId,
                Location = _ctx.RoomObject.ConvertWallPositionToString(),
            },
            ct
        );

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not AddSpamWallPostItInteraction note)
            return false;

        var config = _roomGrain._roomConfig;

        if (
            !StickieColors.IsValid(note.Color)
            || note.Text.Length == 0
            || note.Text.Length > config.StickieTextMaxLength
            || !WallPosition.TryParse(note.Location, out var position)
        )
            return Reject(ctx, interaction, "colour, text or location invalid");

        var definition = _definitionProvider.TryGetDefinitionByName(
            config.SpamWallPostItDefinitionName
        );

        if (definition is null || definition.ProductType != ProductType.Wall)
        {
            _roomGrain._logger.LogWarning(
                "Post-it wall {ItemId} in room {RoomId} cannot create notes: no wall definition named {DefinitionName}",
                _ctx.ObjectId,
                _ctx.RoomId,
                config.SpamWallPostItDefinitionName
            );

            return false;
        }

        var stuffData = _stuffDataFactory.CreateStuffData(StuffDataType.LegacyKey);

        stuffData.SetState(StickieColors.Compose(note.Color.ToUpperInvariant(), note.Text));

        var extraDataJson = JsonSerializer.Serialize(
            new { stuff = JsonSerializer.SerializeToNode(stuffData, stuffData.GetType()) }
        );

        return await _roomGrain.FurniModule.CreateWallItemAsync(
            ctx,
            definition,
            _ctx.RoomObject.OwnerId,
            position,
            extraDataJson,
            stuffData.GetSnapshot(),
            ct
        );
    }
}
