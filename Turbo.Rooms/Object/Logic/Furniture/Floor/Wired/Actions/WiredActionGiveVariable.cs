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
            new WiredParamRule(0), // init value
            new WiredBoolParamRule(false), // override
        ];

    public override int GetMaxVariableIds() => 1;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
            ],
        ];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        [
            new WiredVariableAllInRoomSnapshot()
            {
                ContextType = WiredContextType.AllVariablesInRoom,
                AllVariablesHash = _roomGrain._state.AllVariablesHash,
            },
        ];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = await ctx.GetEffectiveSelectionAsync(this, ct);
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
                        foreach (var playerId in selection.SelectedPlayerIds)
                        {
                            var key = new WiredVariableKey(
                                id,
                                WiredVariableTargetType.User,
                                playerId
                            );

                            await variable.GiveValueAsync(key, value, replace);
                        }

                        break;
                    }
                }
            }
            catch
            {
                continue;
            }
        }

        return true;
    }
}
