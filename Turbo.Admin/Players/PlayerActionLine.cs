using System;
using System.Globalization;
using System.Linq;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Commands;
using Turbo.Primitives.Commands;

namespace Turbo.Admin.Players;

/// <summary>
/// A panel action on a player as the operator command line that does it: <c>ban alice 7d spam</c>.
/// The panel acts on players through the hotel's own commands, so the same nodes, guards (nobody
/// sanctions a player who outranks them), notices and command log apply as when typed in game;
/// this only writes the line, from fields it checks. Null, with a reason, for one it will not
/// write.
/// </summary>
public static class PlayerActionLine
{
    public static (string? Line, string? Error) Build(
        string playerName,
        PlayerActionRequest request
    )
    {
        // The commands take a player by name, and read @ as a selector (@room) and a space as the
        // next argument: a name like that is the console's to deal with.
        if (
            playerName.Length == 0
            || playerName[0] == PlayerTarget.SELECTOR_PREFIX
            || playerName.Any(char.IsWhiteSpace)
        )
            return (
                null,
                $"{playerName} can't be named in a command from the panel. Use the console."
            );

        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();

        return action switch
        {
            "ban" => WithDuration(
                "ban",
                playerName,
                request.Duration,
                CommandText.OneLine(request.Reason)
            ),
            "unban" => ($"unban {playerName}", null),
            "silence" => WithDuration("silence", playerName, request.Duration, null),
            "unsilence" => ($"unsilence {playerName}", null),
            "tradelock" => WithDuration("tradelock", playerName, request.Duration, null),
            "untradelock" => ($"untradelock {playerName}", null),
            "disconnect" => ($"disconnect {playerName}", null),
            "warn" => WithMessage("warn", playerName, request.Reason),
            "alert" => WithMessage("alert", playerName, request.Reason),
            "give" => Give(playerName, request.Currency, request.Amount),
            "givebadge" => WithBadge("givebadge", playerName, request.Badge),
            "takebadge" => WithBadge("takebadge", playerName, request.Badge),
            "giveitem" => GiveItem(playerName, request.Furni, request.Amount),
            _ => (null, $"'{request.Action}' is not something the panel can do to a player."),
        };
    }

    private static (string?, string?) WithDuration(
        string command,
        string name,
        string? duration,
        string? reason
    )
    {
        var text = (duration ?? string.Empty).Trim();

        if (!CommandDuration.TryParse(text, out _))
            return (null, $"'{duration}' is not a duration like 30m, 12h, 7d, 2w or perm.");

        return (
            reason is null ? $"{command} {name} {text}" : $"{command} {name} {text} {reason}",
            null
        );
    }

    private static (string?, string?) WithMessage(string command, string name, string? message) =>
        CommandText.OneLine(message) is { } text
            ? ($"{command} {name} {text}", null)
            : (null, "Write the message to send.");

    private static (string?, string?) WithBadge(string command, string name, string? badge)
    {
        var code = (badge ?? string.Empty).Trim();

        // One word of letters, digits and _, as badge codes are, so it cannot add arguments.
        if (code.Length == 0 || !code.All(x => char.IsAsciiLetterOrDigit(x) || x == '_'))
            return (null, "Write a badge code of letters, digits and _, like ADM or ACH_Login1.");

        return ($"{command} {name} {code}", null);
    }

    private static (string?, string?) GiveItem(string name, string? furni, int? count)
    {
        var className = (furni ?? string.Empty).Trim();

        // A class name as the furniture data has it (throne, rare_dragonlamp*4), one word, so it
        // cannot add arguments or name a selector.
        if (
            className.Length == 0
            || !className.All(x => char.IsAsciiLetterOrDigit(x) || x is '_' or '*' or '-' or '.')
        )
            return (null, "Write the furniture's class name, like throne or rare_dragonlamp*4.");

        if (count is not { } value || value < 1)
            return (null, "Give at least one.");

        return (
            $"giveitem {name} {className} {value.ToString(CultureInfo.InvariantCulture)}",
            null
        );
    }

    private static (string?, string?) Give(string name, string? currency, int? amount)
    {
        var kind = (currency ?? string.Empty).Trim().ToLowerInvariant();

        if (kind.Length == 0 || !kind.All(char.IsLetterOrDigit))
            return (null, "Choose a currency.");

        if (amount is not { } value || value == 0)
            return (null, "Give an amount other than 0; a negative amount takes.");

        return ($"give {name} {kind} {value.ToString(CultureInfo.InvariantCulture)}", null);
    }
}
