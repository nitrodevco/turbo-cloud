using System.Globalization;
using Turbo.Admin.Api.Contracts;

namespace Turbo.Admin.Commands;

/// <summary>
/// A panel action on the whole hotel as the operator command line that does it: a hotel alert,
/// maintenance, a shutdown. The same command as typed in game, so the same nodes, countdowns,
/// confirmation and command log apply; this only writes the line, from fields it checks. Null,
/// with a reason, for one it will not write.
/// </summary>
public static class HotelActionLine
{
    public static (string? Line, string? Error) Build(HotelActionRequest request)
    {
        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();

        return action switch
        {
            "alert" => CommandText.OneLine(request.Message) is { } message
                ? ($"hotelalert {message}", null)
                : (null, "Write the message to send."),
            "maintenance" => Countdown("maintenance", request),
            "maintenance-off" => ("maintenance off", null),
            "shutdown" => Countdown("shutdown", request),
            "shutdown-cancel" => ("shutdown cancel", null),
            _ => (null, $"'{request.Action}' is not something the panel can do to the hotel."),
        };
    }

    /// <summary>A countdown in whole minutes, the command checking its upper limit; then the reason.</summary>
    private static (string?, string?) Countdown(string command, HotelActionRequest request)
    {
        if (request.Minutes is not { } minutes || minutes < 0)
            return (null, "Say how many minutes, 0 or more.");

        var line = $"{command} {minutes.ToString(CultureInfo.InvariantCulture)}";

        return (
            CommandText.OneLine(request.Message) is { } reason ? $"{line} {reason}" : line,
            null
        );
    }
}
