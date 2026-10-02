using System.Collections.Frozen;
using System.Collections.Generic;

namespace Turbo.Primitives.Commands;

/// <summary>
/// The replies core itself makes, as hotel text keys with the text used when the hotel has none.
/// A hotel's texts of the same key win, so every reply can be reworded or translated without
/// touching code. A command's own statuses are <c>command.&lt;name&gt;.&lt;status&gt;</c>.
/// </summary>
public static class CommandReplyKeys
{
    private const string ERROR_PREFIX = "command.error.";

    public const string NO_PERMISSION = "command.error.no_permission";
    public const string NEEDS_ROOM_LEVEL = "command.error.room_level";
    public const string USAGE = "command.error.usage";
    public const string TARGET_NOT_FOUND = "command.error.target_not_found";
    public const string BAD_VALUE = "command.error.bad_value";
    public const string FLOOD = "command.error.flood";
    public const string FAILED = "command.error.failed";
    public const string VETOED = "command.error.vetoed";
    public const string PLAYER_NOT_FOUND = "command.error.player_not_found";
    public const string SELECTOR_REFUSED = "command.error.selector_refused";
    public const string SELECTOR_UNKNOWN = "command.error.selector_unknown";
    public const string NEEDS_ROOM = "command.error.needs_room";
    public const string CONFIRM_PROMPT = "command.error.confirm_prompt";
    public const string CONFIRM_SELECTOR = "command.error.confirm_selector";
    public const string NOTHING_TO_CONFIRM = "command.error.nothing_to_confirm";
    public const string CONFIRM_ELSEWHERE = "command.error.confirm_elsewhere";
    public const string BATCH_RESULT = "command.error.batch_result";
    public const string TOO_LONG = "command.error.too_long";
    public const string BUSY = "command.error.busy";
    public const string BAD_QUOTE = "command.error.quote";
    public const string CONSTRAINT = "command.error.constraint";
    public const string NOTICE_FAILED = "command.error.notice_failed";
    public const string NOTICE_OFFLINE = "command.error.notice_offline";

    public static string ForCommand(string name, string status) => $"command.{name}.{status}";

    /// <summary>
    /// The status part of a command's description text: <c>command.&lt;name&gt;.description</c>
    /// rewords what <c>:commands</c> and a client's completion say a command does.
    /// </summary>
    public const string DESCRIPTION = "description";

    /// <summary>
    /// Whether a result's status is already one of the shared keys above, as when a command hands
    /// back the refusal of a target that was not found, rather than a status of its own.
    /// </summary>
    public static bool IsShared(string status) =>
        status.StartsWith(ERROR_PREFIX, System.StringComparison.Ordinal);

    public static IReadOnlyDictionary<string, string> Defaults { get; } =
        new Dictionary<string, string>
        {
            [NOTICE_FAILED] =
                "The action result is unchanged, but delivery of %0% target notice(s) could not be confirmed.",
            [NOTICE_OFFLINE] = "No notice was sent to %0% offline player(s).",
            [TOO_LONG] = "That command is too long.",
            [BUSY] = "Too many commands are waiting. Try again when they finish.",
            [BAD_QUOTE] = "Invalid quoted argument at character %0%. Usage: %1%",
            [CONSTRAINT] = "%0% does not meet the limits for %1% (%2%).",
            [NO_PERMISSION] = "You can't use that command.",
            [NEEDS_ROOM_LEVEL] = "You need more control of this room to use that command.",
            [USAGE] = "Usage: %0%",
            [TARGET_NOT_FOUND] = "There is nobody called %0% in this room.",
            [BAD_VALUE] = "%0% is not a valid %1%. Usage: %2%",
            [FLOOD] = "You're sending commands too fast.",
            [FAILED] = "That command failed.",
            [VETOED] = "You can't use that command right now.",
            [PLAYER_NOT_FOUND] = "There is no player called %0%.",
            [SELECTOR_REFUSED] = "You can't use %0% with that command.",
            [SELECTOR_UNKNOWN] = "%0% is not a group of players. Use @room or @online.",
            [NEEDS_ROOM] = "That command only works in a room.",
            [CONFIRM_PROMPT] = "%0% Type :confirm within %1% seconds to go ahead.",
            [CONFIRM_SELECTOR] = "That reaches %0% players.",
            [NOTHING_TO_CONFIRM] = "There is nothing to confirm.",
            [CONFIRM_ELSEWHERE] = "That was typed in another room. Type it again here.",
            [BATCH_RESULT] =
                "Players: %0% completed, %1% partial, %2% failed, %3% unattempted. Operations: %4% succeeded, %5% refused, %6% indeterminate, %7% unattempted.",
        }.ToFrozenDictionary();
}
