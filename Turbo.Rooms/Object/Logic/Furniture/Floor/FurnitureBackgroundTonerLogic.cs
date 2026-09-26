using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The room background toner: int data <c>[state, hue, saturation, lightness]</c>, each channel
/// 0..255, as the client's background colour logic reads it. Rights to edit and to toggle.
/// </summary>
[RoomObjectLogic("background_toner")]
public class FurnitureBackgroundTonerLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const int OFF = 0;
    private const int ON = 1;
    private const int MIN_CHANNEL = 0;
    private const int MAX_CHANNEL = 255;
    private const int STATE_INDEX = 0;
    private const int HUE_INDEX = 1;
    private const int SATURATION_INDEX = 2;
    private const int LIGHTNESS_INDEX = 3;

    protected override StuffDataType _stuffDataType => StuffDataType.NumberKey;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (StuffData is not INumberStuffData numbers || !await HasRightsAsync(ctx))
            return;

        var (hue, saturation, lightness) = Channels(numbers);
        var next = numbers.ValueAt(STATE_INDEX) == ON ? OFF : ON;

        await SetNumberDataAsync([next, hue, saturation, lightness]);
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (
            interaction is not SetBackgroundTonerInteraction toner
            || StuffData is not INumberStuffData numbers
        )
            return false;

        if (!await HasRightsAsync(ctx))
            return Reject(ctx, interaction, "no rights");

        if (
            toner.Hue is < MIN_CHANNEL or > MAX_CHANNEL
            || toner.Saturation is < MIN_CHANNEL or > MAX_CHANNEL
            || toner.Lightness is < MIN_CHANNEL or > MAX_CHANNEL
        )
            return Reject(ctx, interaction, "channel out of range");

        return await SetNumberDataAsync([
            numbers.ValueAt(STATE_INDEX),
            toner.Hue,
            toner.Saturation,
            toner.Lightness,
        ]);
    }

    private static (int Hue, int Saturation, int Lightness) Channels(INumberStuffData numbers) =>
        (
            numbers.ValueAt(HUE_INDEX),
            numbers.ValueAt(SATURATION_INDEX),
            numbers.ValueAt(LIGHTNESS_INDEX)
        );
}
