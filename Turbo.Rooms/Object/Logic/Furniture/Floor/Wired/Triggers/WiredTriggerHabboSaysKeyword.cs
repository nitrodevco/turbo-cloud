using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>
/// Fires on chat. Params: hide the message (kept for the client), the match mode (contains,
/// exact, every word) and owner only. The string param is the keyword.
/// </summary>
[RoomObjectLogic("wf_trg_says_something")]
public class WiredTriggerHabboSaysKeyword(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int HIDE_PARAM_INDEX = 0;
    private const int MATCH_MODE_PARAM_INDEX = 1;
    private const int OWNER_ONLY_PARAM_INDEX = 2;

    private const int MATCH_CONTAINS = 0;
    private const int MATCH_EXACT = 1;
    private const int MATCH_ALL_WORDS = 2;

    public override int WiredCode => (int)WiredTriggerType.AVATAR_SAYS_SOMETHING;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PlayerChatEvent)];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(0, 2, 0),
            new WiredBoolParamRule(false),
        ];

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not PlayerChatEvent chatEvt || chatEvt.ChatType == RoomChatType.Whisper)
            return Task.FromResult(false);

        var keyword = _wiredData.StringParam?.Trim() ?? string.Empty;

        if (keyword.Length == 0)
            return Task.FromResult(false);

        var text = chatEvt.Text.Trim();

        return Task.FromResult(
            GetIntParamOrDefault(MATCH_MODE_PARAM_INDEX, MATCH_CONTAINS) switch
            {
                MATCH_EXACT => string.Equals(text, keyword, StringComparison.OrdinalIgnoreCase),
                MATCH_ALL_WORDS => keyword
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase)),
                _ => text.Contains(keyword, StringComparison.OrdinalIgnoreCase),
            }
        );
    }

    public override async Task<bool> CanTriggerAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.Event is not PlayerChatEvent chatEvt)
            return false;

        if (!GetIntParamOrDefault(OWNER_ONLY_PARAM_INDEX, false))
            return true;

        var snapshot = await _ctx.Room.GetSnapshotAsync(ct);

        return snapshot.OwnerId == chatEvt.PlayerId;
    }

    /// <summary>Whether the keyword should not be shown to the room (honoured by the chat path).</summary>
    public bool ShouldHideMessage() => GetIntParamOrDefault(HIDE_PARAM_INDEX, false);
}
