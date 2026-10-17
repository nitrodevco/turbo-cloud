using System;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Turbo.Gamedata.Habbo.Client.Theme;

/// <summary>
/// Numbers as Studio's theme build reads and writes them, in JavaScript's terms: an attribute read
/// with <c>Number(...)</c>, a number written into a sprite's name with <c>String(...)</c>, and one
/// written into JSON, where <c>NaN</c> becomes <c>null</c>.
/// </summary>
internal static partial class ThemeNumbers
{
    /// <summary>
    /// <c>Number(value)</c>: blank is 0, a missing value and anything that is not a number are
    /// <c>NaN</c>; decimal, exponent and <c>0x</c> / <c>0o</c> / <c>0b</c> forms are read.
    /// </summary>
    public static double Parse(string? value)
    {
        if (value is null)
            return double.NaN;

        var text = value.Trim().Trim('\uFEFF');

        if (text.Length == 0)
            return 0;

        switch (text)
        {
            case "Infinity" or "+Infinity":
                return double.PositiveInfinity;
            case "-Infinity":
                return double.NegativeInfinity;
        }

        if (text.Length > 2 && text[0] == '0' && char.IsAsciiLetter(text[1]))
        {
            var radix = char.ToLowerInvariant(text[1]) switch
            {
                'x' => 16,
                'o' => 8,
                'b' => 2,
                _ => 0,
            };

            if (radix == 0)
                return double.NaN;

            var result = 0d;

            foreach (var digit in text.AsSpan(2))
            {
                var at = HexDigitValue(digit);

                if (at < 0 || at >= radix)
                    return double.NaN;

                result = (result * radix) + at;
            }

            return result;
        }

        return DecimalNumber().IsMatch(text)
            ? double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)
            : double.NaN;
    }

    /// <summary>
    /// <c>String(value)</c> for the numbers a skin gives (a shade such as <c>0.5</c>): integers
    /// without a fraction, the rest as the shortest form that reads back.
    /// </summary>
    public static string Format(double value)
    {
        if (double.IsNaN(value))
            return "NaN";

        if (double.IsInfinity(value))
            return value > 0 ? "Infinity" : "-Infinity";

        if (value == Math.Floor(value) && Math.Abs(value) < 1e21)
            return ((decimal)value).ToString(CultureInfo.InvariantCulture);

        return value.ToString("R", CultureInfo.InvariantCulture).Replace("E", "e");
    }

    /// <summary>A number as <c>JSON.stringify</c> writes it: <c>NaN</c> and infinities as null.</summary>
    public static JsonNode? Json(double value)
    {
        if (!double.IsFinite(value))
            return null;

        if (value == Math.Floor(value) && Math.Abs(value) < 9e15)
            return JsonValue.Create((long)value);

        return JsonValue.Create(value);
    }

    private static int HexDigitValue(char digit) =>
        digit switch
        {
            >= '0' and <= '9' => digit - '0',
            >= 'a' and <= 'f' => digit - 'a' + 10,
            >= 'A' and <= 'F' => digit - 'A' + 10,
            _ => -1,
        };

    [GeneratedRegex(@"^[+-]?([0-9]+\.?[0-9]*([eE][+-]?[0-9]+)?|\.[0-9]+([eE][+-]?[0-9]+)?)$")]
    private static partial Regex DecimalNumber();
}
