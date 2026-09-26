using System;
using System.Globalization;

namespace Turbo.Primitives.Texts;

/// <summary>
/// How a date is written for the client to show as-is: group created, member since, trophy,
/// friend furni, profile created. The client never parses these, it prints them, so they only
/// look like one hotel if every one of them is written here. Four copies of the format and a
/// profile that used another were how it drifted.
/// </summary>
public static class ClientDates
{
    public const string DISPLAY_FORMAT = "dd-MM-yyyy";

    public static string Format(DateTime value) =>
        value.ToString(DISPLAY_FORMAT, CultureInfo.InvariantCulture);
}
