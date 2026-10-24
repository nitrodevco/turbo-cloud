using System;
using System.Globalization;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Wired;

/// <summary>
/// Shared by the "performs action" trigger, condition and selector: whether an action (with its
/// sign or dance value) matches the box configuration, and whether an avatar is currently in
/// the state an action describes.
/// </summary>
public static class WiredAvatarActionMatcher
{
    /// <summary>The sign or dance id the string param narrows to, or null for any.</summary>
    public static int? ParseExtra(WiredAvatarActionType wanted, string? stringParam)
    {
        if (string.IsNullOrWhiteSpace(stringParam))
            return null;

        var text = stringParam.Trim();

        if (
            wanted == WiredAvatarActionType.Dance
            && text.StartsWith("dance ", StringComparison.OrdinalIgnoreCase)
        )
            text = text[6..].Trim();

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Puts what the room reported into the numbering of the wired editor's action list. False
    /// for something that list has no entry for (crying), which then matches no box.
    /// </summary>
    public static bool TryTranslate(
        AvatarPerformsActionEvent evt,
        out WiredAvatarActionType action,
        out int value
    )
    {
        value = 0;

        WiredAvatarActionType? translated = evt.ActionType switch
        {
            AvatarActionType.Expression => (AvatarExpressionType)evt.Value switch
            {
                AvatarExpressionType.Wave => WiredAvatarActionType.Wave,
                AvatarExpressionType.Blow => WiredAvatarActionType.Blow,
                AvatarExpressionType.Laugh => WiredAvatarActionType.Laugh,
                AvatarExpressionType.Respect => WiredAvatarActionType.Respect,
                AvatarExpressionType.Idle => WiredAvatarActionType.Sleep,
                AvatarExpressionType.Jump => WiredAvatarActionType.Jump,
                _ => null,
            },
            AvatarActionType.Posture => (AvatarPostureType)evt.Value == AvatarPostureType.Sit
                ? WiredAvatarActionType.Sit
                : WiredAvatarActionType.Stand,
            AvatarActionType.Dance => WiredAvatarActionType.Dance,
            AvatarActionType.Sign => WiredAvatarActionType.Sign,
            _ => null,
        };

        action = translated ?? default;

        // Only a dance and a sign have an id a box can narrow to.
        if (translated is WiredAvatarActionType.Dance or WiredAvatarActionType.Sign)
            value = evt.Value;

        return translated is not null;
    }

    public static bool Matches(
        WiredAvatarActionType actual,
        int actualValue,
        WiredAvatarActionType wanted,
        string? stringParam
    )
    {
        if (actual != wanted)
            return false;

        var extra = ParseExtra(wanted, stringParam);

        return extra is null || extra == actualValue;
    }

    /// <summary>
    /// Whether the avatar is in the state the action leaves behind (sitting, dancing...), or made
    /// the expression it names (a wave, a blow kiss...) no longer ago than the expression lasts.
    /// Users By Action picked a user 1.1 s after a wave and not 5.3 s after it (official client
    /// capture 2026-10-24, W4 lab selector.9.R5, 1/1 each); the client shows a wave for 5 s
    /// (<c>AvatarAction.getExpressionTime</c>, WIN63-202610201032 AvatarAction.as:103), and the
    /// other expressions are held as long as the client shows them (inference: only the wave was
    /// measured).
    /// </summary>
    public static bool IsPerforming(
        IRoomAvatar avatar,
        WiredAvatarActionType wanted,
        string? stringParam,
        long nowMs
    )
    {
        if (ExpressionOf(wanted) is { } expression)
            return avatar.Expression == expression
                && nowMs - avatar.ExpressionAtMs < ShownMs(expression);

        var extra = ParseExtra(wanted, stringParam);

        return wanted switch
        {
            WiredAvatarActionType.Sit => avatar.HasStatus(AvatarStatusType.Sit),
            WiredAvatarActionType.Lay => avatar.HasStatus(AvatarStatusType.Lay),
            WiredAvatarActionType.Stand => !avatar.HasStatus(
                AvatarStatusType.Sit,
                AvatarStatusType.Lay
            ),
            WiredAvatarActionType.Sleep => avatar.IsIdle,
            WiredAvatarActionType.Awake => !avatar.IsIdle,
            WiredAvatarActionType.Sign => avatar.Statuses.TryGetValue(
                AvatarStatusType.Sign,
                out var sign
            ) && (extra is null || sign == extra.Value.ToString()),
            WiredAvatarActionType.Dance => avatar.DanceType != AvatarDanceType.None
                && (extra is null || (int)avatar.DanceType == extra),
            _ => false,
        };
    }

    private static AvatarExpressionType? ExpressionOf(WiredAvatarActionType action) =>
        action switch
        {
            WiredAvatarActionType.Wave => AvatarExpressionType.Wave,
            WiredAvatarActionType.Blow => AvatarExpressionType.Blow,
            WiredAvatarActionType.Laugh => AvatarExpressionType.Laugh,
            WiredAvatarActionType.Respect => AvatarExpressionType.Respect,
            WiredAvatarActionType.Jump => AvatarExpressionType.Jump,
            _ => null,
        };

    /// <summary>How long the client shows an expression (AvatarAction.as:103, getExpressionTime).</summary>
    private static int ShownMs(AvatarExpressionType expression) =>
        expression switch
        {
            AvatarExpressionType.Wave => 5000,
            AvatarExpressionType.Blow => 1400,
            AvatarExpressionType.Jump => 700,
            _ => 2000,
        };
}
