using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Orleans;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// "Variable Add-on: Share" (<c>wf_xtra_var_share</c>, AddonCodes.VARIABLE_ADDON_SHARE 1003,
/// AS3 <c>VariableAddonShareAddon</c> WIN63-202610201032): placed on a permanently shared
/// variable, it shares the variable add-ons of the ticked categories on that stack with the
/// rooms using the variable through "From Another Room" (Wired Faculty 19/10/2026;
/// <c>addon_share.usage_info</c>). Its "Synchronize" button sends
/// <c>SynchronizeSharedAddons</c>.
/// </summary>
[RoomObjectLogic("wf_xtra_var_share")]
public class WiredAddonVariableShare(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredAddonVariableSharing(grainFactory, stuffDataFactory, ctx)
{
    private const string SECTION = "shared_addons";

    /// <summary>What a Synchronize took, as kept with the box (a value's text by the value).</summary>
    private sealed record SynchronizedData(
        WiredVariableAddonCategories Categories,
        bool HasTextConnector,
        Dictionary<long, string> TextConnectors,
        string? TextConnectorFallback,
        List<SharedWiredVariableAddonSnapshot> Addons
    );

    private SharedWiredVariableAddonsSnapshot? _synchronized;
    private bool _synchronizedRead;

    public override int WiredCode => (int)WiredAddonType.VARIABLE_ADDON_SHARE;

    /// <summary>The shared variable this add-on stands on, or null.</summary>
    public FurnitureWiredVariableLogic? GetSharedVariable() =>
        GetVariableBoxOnTile() is { IsShared: true } variable ? variable : null;

    /// <summary>What the last Synchronize took off the stack; null before the first.</summary>
    public SharedWiredVariableAddonsSnapshot? Synchronized
    {
        get
        {
            if (_synchronizedRead)
                return _synchronized;

            _synchronizedRead = true;
            _synchronized = FurnitureExtraDataSections.Read<SynchronizedData>(
                _ctx.RoomObject.ExtraData,
                SECTION,
                _roomGrain._logger
            )
                is { } data
                ? new SharedWiredVariableAddonsSnapshot
                {
                    Categories = data.Categories,
                    HasTextConnector = data.HasTextConnector,
                    TextConnectors = data.TextConnectors.ToDictionary(
                        x => new WiredVariableValue(x.Key),
                        x => x.Value
                    ),
                    TextConnectorFallback = data.TextConnectorFallback,
                    Addons = [.. data.Addons],
                }
                : null;

            return _synchronized;
        }
    }

    /// <summary>Keeps <paramref name="addons"/> as what this add-on shares, until the next Synchronize.</summary>
    public void Synchronize(SharedWiredVariableAddonsSnapshot addons)
    {
        _synchronized = addons;
        _synchronizedRead = true;
        _ctx.RoomObject.ExtraData.UpdateSection(
            SECTION,
            JsonSerializer.SerializeToNode(
                new SynchronizedData(
                    addons.Categories,
                    addons.HasTextConnector,
                    addons.TextConnectors.ToDictionary(x => x.Key.Value, x => x.Value),
                    addons.TextConnectorFallback,
                    [.. addons.Addons]
                )
            )
        );
    }
}
