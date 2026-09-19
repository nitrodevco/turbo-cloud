using System;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
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

        return int.TryParse(text, out var value) ? value : null;
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

    /// <summary>Whether the avatar is in the state the action leaves behind (sitting, dancing...).</summary>
    public static bool IsPerforming(
        IRoomAvatar avatar,
        WiredAvatarActionType wanted,
        string? stringParam
    )
    {
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
            WiredAvatarActionType.Wave => avatar.HasStatus(AvatarStatusType.Wave),
            WiredAvatarActionType.Sign => avatar.Statuses.TryGetValue(
                AvatarStatusType.Sign,
                out var sign
            ) && (extra is null || sign == extra.Value.ToString()),
            WiredAvatarActionType.Dance => avatar is IRoomPlayer player
                && player.DanceType != AvatarDanceType.None
                && (extra is null || (int)player.DanceType == extra),
            _ => false,
        };
    }
}
