using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_user")]
public class WiredVariableUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.User;

    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.User;
    protected override int? AvailabilityParamIndex => 0;
    protected override WiredAvailabilityType AvailabilityType =>
        _wiredData.GetIntParam<WiredAvailabilityType>(0);

    // A variable with a value also tells when it was last written, one without only when it was
    // made: Habbo lists every furni, user and context variable a box makes with
    // canReadLastUpdateTime equal to hasValue (official client capture, 2026-10-13..10-25), and the
    // Variable Age editor greys "Last update time" out without it (conditions/VariableAge.as:91-105).
    protected override WiredVariableFlags Flags =>
        (
            _wiredData.GetIntParam<bool>(1)
                ? WiredVariableFlags.HasValue
                    | WiredVariableFlags.CanWriteValue
                    | WiredVariableFlags.CanReadLastUpdateTime
                : WiredVariableFlags.None
        )
        | WiredVariableFlags.CanCreateAndDelete
        | WiredVariableFlags.CanInterceptChanges
        | WiredVariableFlags.CanReadCreationTime;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredAvailabilityType>(
                WiredAvailabilityType.UserActive,
                WiredAvailabilityType.UserActive,
                WiredAvailabilityType.Persistent,
                WiredAvailabilityType.Shared
            )
            {
                ErrorKey = WiredSaveErrors.VARIABLE_INVALID_AVAILABILITY,
            },
            new WiredBoolParamRule(false),
        ];
}
