using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
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

    // A Variable Capturer's marker in the keyword, "#(name)".
    private static readonly Regex MarkerPattern = new(
        @"#\([^()]*\)",
        RegexOptions.CultureInvariant
    );

    public override int WiredCode => (int)WiredTriggerType.AVATAR_SAYS_SOMETHING;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PlayerChatEvent)];

    // A fresh box hides the message (official client capture: defaultIntParams [0,0,1]).
    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(0, 2, 0),
            new WiredBoolParamRule(true),
        ];

    /// <summary>
    /// A keyword holding the same "#(name)" marker twice is refused with
    /// <c>wiredfurni.error.capturer.duplicate</c>, in any match mode and whether or not a capturer
    /// has the name: Habbo refused "#(a) #(a)" and "#(a) x #(a)" (exact) and "#(a) #(a)"
    /// (contains) when the trigger was saved (official client capture 2026-10-14,
    /// NOTES-iv-save-errors). Names are compared case-insensitively (inference: the client
    /// lowercases placeholder names, PlaceholderNameSection).
    /// </summary>
    public override Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        var markers = CaptureMarker
            .Matches(update.StringParam ?? string.Empty)
            .Select(x => x.Groups[1].Value);

        if (markers.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new WiredSaveRefusedException(WiredSaveErrors.CAPTURER_DUPLICATE);

        return base.ApplyWiredUpdateAsync(ctx, update, ct);
    }

    private static readonly Regex CaptureMarker = new(@"#\(([^)]*)\)", RegexOptions.Compiled);

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        // A whisper counts unless its speaker opted out (RoomChatSystem decides WiredMayRead).
        if (evt is not PlayerChatEvent chatEvt || !chatEvt.WiredMayRead)
            return Task.FromResult(false);

        var mode = GetIntParamOrDefault(MATCH_MODE_PARAM_INDEX, MATCH_CONTAINS);

        // Anything said will do, so there is no keyword to check: the editor disables the box.
        if (mode == MATCH_ANY_TEXT)
            return Task.FromResult(true);

        var keyword = GetStringParam();

        // The editor takes far more than Habbo fires on: a longer pattern never matches.
        if (
            keyword.Length == 0
            || keyword.Length > _roomGrain._wiredConfig.ChatTriggerPatternMaxLength
        )
            return Task.FromResult(false);

        var text = chatEvt.Text.Trim();

        if (MarkerPattern.IsMatch(keyword))
            return Task.FromResult(TryCapture(text, out _));

        return Task.FromResult(
            mode == MATCH_EXACT
                ? string.Equals(text, keyword, StringComparison.OrdinalIgnoreCase)
                : text.Contains(keyword, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// What the player typed for each "#(name)" marker of the keyword, with the Variable Capturer
    /// on this stack that takes it. A marker stands for the word typed there ("price #(p)"
    /// matches "price 250", Wired Faculty tutorial "Advanced Automatic Shop", 20/03/2026), spaces
    /// for any run of them; in exact mode a marker next to another marker takes one character
    /// ("QR:#(a)#(b)"), and trailing ones may be left empty.
    ///
    /// False when the line does not fire the box: it does not fit the pattern, a marker has no
    /// capturer of its own, or a capturer in text mode has no connector line for what was typed
    /// (official client capture: each of these stops the trigger).
    /// </summary>
    internal bool TryCapture(
        string text,
        out List<(WiredAddonVariableCapturer Capturer, string Typed)> captures
    )
    {
        captures = [];

        var keyword = GetStringParam();
        var markers = MarkerPattern.Matches(keyword);
        var exact = GetIntParamOrDefault(MATCH_MODE_PARAM_INDEX, MATCH_CONTAINS) == MATCH_EXACT;
        var capturers = FurniModule
            .GetFloorItemsOnTile(_ctx.GetTileIdx())
            .Select(item => item.Logic)
            .OfType<WiredAddonVariableCapturer>()
            .ToList();
        var takers = new List<WiredAddonVariableCapturer>(markers.Count);
        var pattern = new StringBuilder(exact ? "^" : string.Empty);
        var end = 0;

        for (var index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];
            var capturer = capturers.FirstOrDefault(x =>
                string.Equals(x.CaptureToken, marker.Value, StringComparison.OrdinalIgnoreCase)
            );

            if (capturer is null)
                return false;

            var nextToMarker =
                (index > 0 && markers[index - 1].Index + markers[index - 1].Length == marker.Index)
                || (
                    index + 1 < markers.Count
                    && marker.Index + marker.Length == markers[index + 1].Index
                );

            takers.Add(capturer);
            pattern
                .Append(Literal(keyword[end..marker.Index]))
                .Append(exact && nextToMarker ? "(.?)" : @"(\S+)");
            end = marker.Index + marker.Length;
        }

        pattern.Append(Literal(keyword[end..])).Append(exact ? "$" : string.Empty);

        Match match;

        try
        {
            // Both the keyword and the chat line come from players, so the match is time boxed.
            match = Regex.Match(
                text,
                pattern.ToString(),
                RegexOptions.IgnoreCase | RegexOptions.Singleline,
                TimeSpan.FromMilliseconds(_roomGrain._wiredConfig.RegexMatchTimeoutMs)
            );
        }
        catch (RegexMatchTimeoutException ex)
        {
            LogWiredDataFault(ex);

            return false;
        }

        if (!match.Success)
            return false;

        for (var index = 0; index < takers.Count; index++)
        {
            // Habbo lowercases the line before it captures (official client capture).
            var typed = match.Groups[index + 1].Value.ToLowerInvariant();

            if (!takers[index].Accepts(typed))
                return false;

            captures.Add((takers[index], typed));
        }

        return true;
    }

    private static string Literal(string text) => Regex.Escape(text).Replace("\\ ", "\\s+");

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        ctx.Event is PlayerChatEvent chatEvt
            ? SpeakerMayTriggerAsync(chatEvt, ct)
            : Task.FromResult(false);

    private async Task<bool> SpeakerMayTriggerAsync(PlayerChatEvent chatEvt, CancellationToken ct)
    {
        if (!GetIntParamOrDefault(OWNER_ONLY_PARAM_INDEX, false))
            return true;

        var snapshot = await _ctx.Room.GetSnapshotAsync(ct);

        return snapshot.OwnerId == chatEvt.PlayerId;
    }

    /// <summary>Whether the keyword should not be shown to the room.</summary>
    public bool ShouldHideMessage() => GetIntParamOrDefault(HIDE_PARAM_INDEX, false);

    /// <summary>
    /// Whether this box takes <paramref name="chatEvt"/> out of the room's chat: it hides, and it
    /// fires for this line and this speaker. The chat path then shows the line to its speaker
    /// alone.
    /// </summary>
    public async Task<bool> HidesAsync(PlayerChatEvent chatEvt, CancellationToken ct) =>
        ShouldHideMessage()
        && await MatchesEventAsync(chatEvt, ct)
        && await SpeakerMayTriggerAsync(chatEvt, ct);

    /// <summary>Whether this box fires for <paramref name="chatEvt"/> and its speaker.</summary>
    public async Task<bool> FiresForAsync(PlayerChatEvent chatEvt, CancellationToken ct) =>
        await MatchesEventAsync(chatEvt, ct) && await SpeakerMayTriggerAsync(chatEvt, ct);
}
