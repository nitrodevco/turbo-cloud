using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;
using Turbo.Rooms.Wired.VariableFx;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;

/// <summary>
/// The variable on this tile as a plain number, with an optional icon beside it. There is no
/// range: the client draws the value digit by digit. The extra int param is where the icon
/// goes, the string param which icon.
/// </summary>
[RoomObjectLogic("wf_xtra_var_fx_number")]
public class WiredAddonVariableFxNumberDisplay(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableFxLogic(grainFactory, stuffDataFactory, ctx)
{
    // The client's icon alignment ids and the words its renderer reads for them.
    private const int ALIGN_LEFT = 0;
    private const int ALIGN_DOUBLE = 2;

    private static readonly string[] ALIGNMENTS = ["left", "right", "double"];

    // The longest icon id the editor offers is well under this.
    private const int ICON_MAX_LENGTH = 32;

    public override int WiredCode => (int)WiredAddonType.VARIABLE_FX_NUMBER_DISPLAY;

    protected override VariableFxCategoryType Category => VariableFxCategoryType.NumberDisplay;

    protected override bool UsesValueRange => false;

    protected override IEnumerable<IWiredParamRule> GetCategoryParamRules() =>
        [new WiredRangeParamRule(ALIGN_LEFT, ALIGN_DOUBLE, ALIGN_LEFT)];

    protected override int GetStringParamMaxLength() => ICON_MAX_LENGTH;

    protected override void AddCategoryExtra(
        IDictionary<string, string> extra,
        VariableFxStyle style,
        int rendererId
    )
    {
        var icon = _wiredData.StringParam?.Trim();

        // An icon the client has no asset for throws in its renderer, so an unknown one is not sent.
        if (!VariableFxIcons.IsKnown(icon))
            return;

        extra[VariableFxStyles.EXTRA_ICON] = icon!;
        extra[VariableFxStyles.EXTRA_ICON_ALIGNMENT] = ALIGNMENTS[
            GetIntParamOrDefault(PARAM_CATEGORY, ALIGN_LEFT)
        ];
    }
}
