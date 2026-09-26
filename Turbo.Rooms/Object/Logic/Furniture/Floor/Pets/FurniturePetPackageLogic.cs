using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A pet package. What is inside is the <see cref="PetPackageData.SECTION"/> of the item's
/// extra data, or of its definition's extra data as the default for that furniture type. The
/// owner double-clicks it to name the pet; naming hatches it into the inventory and destroys
/// the package.
/// </summary>
[RoomObjectLogic("pet_package")]
public class FurniturePetPackageLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    // The client offers opening it to its owner only, and sends a plain use; Nobody keeps the
    // use button away from everyone else, and this lets the owner's use through.
    public override Task<bool> CanUseAsync(ActionContext ctx) => Task.FromResult(IsItemOwner(ctx));

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        var contents = ReadContents();

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new OpenPetPackageRequestedMessageComposer
            {
                ObjectId = _ctx.ObjectId,
                Figure = contents is null ? null : ToFigure(contents),
            },
            ct
        );
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not OpenPetPackageInteraction open)
            return false;

        if (!IsItemOwner(ctx))
            return Reject(ctx, interaction, "not the owner");

        var contents = ReadContents();

        if (contents is null)
            return Reject(ctx, interaction, "package holds no pet");

        var config = _roomGrain._petConfig;
        var status = PetNames.Validate(open.Name, config.NameMinLength, config.NameMaxLength);

        if (status != PetNameValidationType.Ok)
        {
            await SendResultAsync(ctx, status, ct);

            return true;
        }

        var palette = _roomGrain._petBreedProvider.TryGetPalette(
            contents.TypeId,
            contents.PaletteId
        );

        var pet = await _roomGrain
            ._grainFactory.GetInventoryGrain(ctx.PlayerId)
            .CreatePetAsync(
                open.Name.Trim(),
                contents.TypeId,
                contents.PaletteId,
                palette?.BreedId ?? contents.PaletteId,
                contents.Color.ToUpperInvariant(),
                palette?.RarityLevel ?? 0,
                ct
            );

        if (pet is null)
            return Reject(ctx, interaction, "the inventory refused the pet");

        await _roomGrain.ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, ct);
        await SendResultAsync(ctx, PetNameValidationType.Ok, ct);

        return true;
    }

    private Task SendResultAsync(
        ActionContext ctx,
        PetNameValidationType status,
        CancellationToken ct
    ) =>
        _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new OpenPetPackageResultMessageComposer
            {
                ObjectId = _ctx.ObjectId,
                NameValidationStatus = status,
                NameValidationInfo = string.Empty,
            },
            ct
        );

    private PetPackageData? ReadContents()
    {
        var contents = FurnitureExtraDataSections.Read<PetPackageData>(
            _ctx.RoomObject.ExtraData,
            _ctx.Definition.ExtraData,
            PetPackageData.SECTION,
            _roomGrain._logger
        );

        return contents is null || !PetFigure.IsValidColor(contents.Color) ? null : contents;
    }

    private PetFigureSnapshot ToFigure(PetPackageData contents) =>
        new()
        {
            TypeId = contents.TypeId,
            PaletteId = contents.PaletteId,
            Color = contents.Color.ToUpperInvariant(),
            BreedId =
                _roomGrain
                    ._petBreedProvider.TryGetPalette(contents.TypeId, contents.PaletteId)
                    ?.BreedId
                ?? contents.PaletteId,
            CustomParts = [],
        };
}
