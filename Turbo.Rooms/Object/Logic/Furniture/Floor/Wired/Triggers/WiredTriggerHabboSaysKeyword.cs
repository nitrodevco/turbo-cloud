using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

[RoomObjectLogic("wf_trg_says_something")]
public class WiredTriggerHabboSaysKeyword(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int OWNER_ONLY_PARAM_INDEX = 0;

    public override int WiredCode => (int)WiredTriggerType.AVATAR_SAYS_SOMETHING;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PlayerChatEvent)];

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not PlayerChatEvent chatEvt || chatEvt.ChatType == RoomChatType.Whisper)
            return Task.FromResult(false);

        var keyword = _wiredData.StringParam?.Trim() ?? string.Empty;

        if (keyword.Length == 0)
            return Task.FromResult(false);

        return Task.FromResult(chatEvt.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public override async Task<bool> CanTriggerAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.Event is not PlayerChatEvent chatEvt)
            return false;

        if (!GetIsOwnerOnly())
            return true;

        var snapshot = await _ctx.Room.GetSnapshotAsync(ct);

        return snapshot.OwnerId == chatEvt.PlayerId;
    }

    private bool GetIsOwnerOnly() =>
        _wiredData.IntParams.Count > OWNER_ONLY_PARAM_INDEX
        && _wiredData.GetIntParam<bool>(OWNER_ONLY_PARAM_INDEX);
}
