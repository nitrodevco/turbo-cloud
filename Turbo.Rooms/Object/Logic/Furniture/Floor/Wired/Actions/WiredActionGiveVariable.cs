using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

[RoomObjectLogic("wf_act_give_var")]
public class WiredActionGiveVariable(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.GIVE_VARIABLE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredVariableTargetType>(
                WiredVariableTargetType.User,
                WiredVariableTargetType.User,
                WiredVariableTargetType.Furni,
                WiredVariableTargetType.Context
            ),
            new WiredRangeParamRule(0, 0, 0),
            WiredRules.AnyInt(), // init value
            new WiredBoolParamRule(false), // override
        ];

    public override int GetMaxVariableIds() => 1;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var variableIds = _wiredData.VariableIds;

        foreach (var variableId in variableIds)
        {
            try
            {
                var id = WiredVariableId.Parse(variableId);
                var variable = _roomGrain.WiredSystem.GetVariableById(id);

                if (variable is null)
                    continue;

                int value = _wiredData.GetIntParam<int>(2);
                bool replace = _wiredData.GetIntParam<bool>(3);

                switch (_wiredData.GetIntParam<WiredVariableTargetType>(0))
                {
                    case WiredVariableTargetType.Furni:
                    {
                        foreach (var furniId in selection.SelectedFurniIds)
                        {
                            var key = new WiredVariableKey(
                                id,
                                WiredVariableTargetType.Furni,
                                furniId
                            );

                            await variable.GiveValueAsync(key, value, replace);
                        }

                        break;
                    }
                    case WiredVariableTargetType.User:
                    {
                        // A user variable is keyed by the avatar, not by the player.
                        foreach (
                            var targetId in GetTargetIds(WiredVariableTargetType.User, selection)
                        )
                        {
                            var key = new WiredVariableKey(
                                id,
                                WiredVariableTargetType.User,
                                targetId
                            );

                            await variable.GiveValueAsync(key, value, replace);
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogWiredDataFault(ex);

                continue;
            }
        }

        return true;
    }
}
