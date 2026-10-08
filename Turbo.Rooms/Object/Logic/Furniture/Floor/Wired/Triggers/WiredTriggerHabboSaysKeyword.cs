using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires on chat. Params, as the client's editor writes them: owner only, the match mode and
/// hide the message (honoured by the chat path). The string param is the keyword.
///
/// The third mode is "Match all text", and the editor greys the keyword box out for it: it
/// fires on anything that is said. The other two need a keyword and match it against the whole
/// message.
/// </summary>
[RoomObjectLogic("wf_trg_says_something")]
public class WiredTriggerHabboSaysKeyword(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    // Flash's order (triggerconfs AvatarSaysSomething.readIntParamsFromForm): the checkbox with
    // id 0 ("only the owner"), the match mode, then the checkbox with id 1 ("hide").
    private const int OWNER_ONLY_PARAM_INDEX = 0;
    private const int MATCH_MODE_PARAM_INDEX = 1;
    private const int HIDE_PARAM_INDEX = 2;

    private const int MATCH_CONTAINS = 0;
    private const int MATCH_EXACT = 1;
    private const int MATCH_ANY_TEXT = 2;

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

        var mode = GetIntParamOrDefault(MATCH_MODE_PARAM_INDEX, MATCH_CONTAINS);

        // Anything said will do, so there is no keyword to check: the editor disables the box.
        if (mode == MATCH_ANY_TEXT)
            return Task.FromResult(true);

        var keyword = GetStringParam();

        if (keyword.Length == 0)
            return Task.FromResult(false);

        var text = chatEvt.Text.Trim();

        if (CapturePattern(keyword, mode == MATCH_EXACT) is { } pattern)
        {
            try
            {
                return Task.FromResult(
                    Regex.IsMatch(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase,
                        TimeSpan.FromMilliseconds(_roomGrain._wiredConfig.RegexMatchTimeoutMs)
                    )
                );
            }
            catch (RegexMatchTimeoutException ex)
            {
                LogWiredDataFault(ex);

                return Task.FromResult(false);
            }
        }

        return Task.FromResult(
            mode == MATCH_EXACT
                ? string.Equals(text, keyword, StringComparison.OrdinalIgnoreCase)
                : text.Contains(keyword, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// The keyword as a pattern when it holds the "#(name)" token of a Variable Capturer on this
    /// stack: the token stands for the word the player types there ("price #(p)" matches "price
    /// 250", Wired Faculty tutorial "Advanced Automatic Shop", 20/03/2026), spaces for any run of
    /// them. Null for a plain keyword, which is matched as written.
    /// </summary>
    private string? CapturePattern(string keyword, bool exact)
    {
        var names = FurniModule
            .GetFloorItemsOnTile(_ctx.GetTileIdx())
            .Select(item => item.Logic)
            .OfType<WiredAddonVariableCapturer>()
            .Select(capturer => capturer.CaptureToken)
            .Where(token =>
                token.Length > 1 && keyword.Contains(token, StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        if (names.Count == 0)
            return null;

        var pattern = Regex.Escape(keyword).Replace("\\ ", "\\s+");

        foreach (var token in names)
            pattern = Regex.Replace(
                pattern,
                Regex.Escape(Regex.Escape(token)),
                @"\S+",
                RegexOptions.IgnoreCase
            );

        return exact ? $"^{pattern}$" : pattern;
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
