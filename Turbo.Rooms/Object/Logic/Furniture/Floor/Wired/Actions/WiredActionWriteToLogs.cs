using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Writes the string param to the wired log of the room at the level in param 0.</summary>
[RoomObjectLogic("wf_act_log")]
public class WiredActionWriteToLogs(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.WRITE_TO_LOGS;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<WiredLogLevelType>(WiredLogLevelType.Info)];

    protected override int GetStringParamMaxLength() => _roomGrain._wiredConfig.LogMessageMaxLength;

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var message = _wiredData.StringParam?.Trim() ?? string.Empty;

        if (message.Length == 0)
            return false;

        message = await ctx.FormatTextAsync(message, ct);

        _roomGrain.WiredSystem.RecordLog(
            GetIntParamOrDefault(0, WiredLogLevelType.Info),
            message,
            _roomGrain.NowMs()
        );

        return true;
    }
}
