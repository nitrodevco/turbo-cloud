using System;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Turbo.Assets.Conversion;

/// <summary>
/// Numbers read from Habbo's XML the way the converter the client was built against read them
/// (JavaScript's <c>parseInt</c> and <c>parseFloat</c>): the number at the start of the text, the
/// rest ignored - <c>"64a"</c> is 64 - and no number is NaN, which JSON writes as <c>null</c>. The
/// client reads the bundles that converter wrote, so these read alike.
/// </summary>
internal static class JsValues
{
    /// <summary><c>parseInt(text)</c>: null for NaN.</summary>
    public static double? ParseInt(string text, int radix = 10)
    {
        var span = text.AsSpan().TrimStart();
        var negative = false;

        if (span.Length > 0 && (span[0] == '-' || span[0] == '+'))
        {
            negative = span[0] == '-';
            span = span[1..];
        }

        if (radix == 10 && span.Length > 1 && span[0] == '0' && (span[1] == 'x' || span[1] == 'X'))
        {
            radix = 16;
            span = span[2..];
        }

        double value = 0;
        var digits = 0;

        foreach (var c in span)
        {
            var digit = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'z' => c - 'a' + 10,
                >= 'A' and <= 'Z' => c - 'A' + 10,
                _ => 99,
            };

            if (digit >= radix)
                break;

            value = (value * radix) + digit;
            digits++;
        }

        return digits == 0 ? null
            : negative ? -value
            : value;
    }

    /// <summary><c>parseFloat(text)</c>: null for NaN.</summary>
    public static double? ParseFloat(string text)
    {
        var span = text.AsSpan().TrimStart();
        var end = 0;

        if (end < span.Length && (span[end] == '-' || span[end] == '+'))
            end++;

        var start = end;

        if (span[end..].StartsWith("Infinity", StringComparison.Ordinal))
            return span[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;

        while (end < span.Length && char.IsAsciiDigit(span[end]))
            end++;

        if (end < span.Length && span[end] == '.')
        {
            end++;

            while (end < span.Length && char.IsAsciiDigit(span[end]))
                end++;
        }

        // A lone sign or dot is not a number.
        if (end == start || (end == start + 1 && span[start] == '.'))
            return null;

        if (end < span.Length && (span[end] == 'e' || span[end] == 'E'))
        {
            var exponent = end + 1;

            if (exponent < span.Length && (span[exponent] == '-' || span[exponent] == '+'))
                exponent++;

            var digits = exponent;

            while (digits < span.Length && char.IsAsciiDigit(span[digits]))
                digits++;

            if (digits > exponent)
                end = digits;
        }

        return double.Parse(span[..end], NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A number as JavaScript's <c>JSON.stringify</c> writes it - plain from 1e-7 up to 1e21
    /// (<c>0.000001</c>, not .NET's <c>1E-06</c>), with an exponent past that - and NaN (null) as
    /// <c>null</c>.
    /// </summary>
    public static JsonNode? Number(double? value)
    {
        if (value is not { } number || !double.IsFinite(number))
            return null;

        // JSON.stringify writes -0 (parseInt("-0")) as 0; .NET would write -0.
        if (number == 0)
            number = 0;

        // Whole numbers below 1e15 write alike either way (.NET turns to an exponent past them).
        if (number == Math.Floor(number) && Math.Abs(number) < 1e15)
            return JsonValue.Create(number);

        return JsonNode.Parse(Format(number));
    }

    /// <summary>Number.prototype.toString for a finite number: the shortest digits that read back.</summary>
    internal static string Format(double number)
    {
        if (number == 0)
            return "0";

        var negative = number < 0;
        // The shortest round-tripping digits, then placed as JavaScript places them.
        var shortest = Math.Abs(number).ToString("R", CultureInfo.InvariantCulture);
        var (digits, exponent) = Decompose(shortest);
        var n = exponent + 1; // where the decimal point falls after the first digit
        var k = digits.Length;
        string text;

        if (k <= n && n <= 21)
            text = digits + new string('0', n - k);
        else if (0 < n && n <= 21)
            text = digits[..n] + "." + digits[n..];
        else if (-6 < n && n <= 0)
            text = "0." + new string('0', -n) + digits;
        else
        {
            var e = n - 1;
            var mantissa = k == 1 ? digits : digits[..1] + "." + digits[1..];

            text =
                mantissa
                + "e"
                + (e >= 0 ? "+" : "-")
                + Math.Abs(e).ToString(CultureInfo.InvariantCulture);
        }

        return negative ? "-" + text : text;
    }

    /// <summary>A number's significant digits (no leading or trailing zeros) and the exponent of the first.</summary>
    private static (string Digits, int Exponent) Decompose(string text)
    {
        var e = 0;
        var mark = text.IndexOfAny(['E', 'e']);

        if (mark >= 0)
        {
            e = int.Parse(
                text[(mark + 1)..],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture
            );
            text = text[..mark];
        }

        var dot = text.IndexOf('.', StringComparison.Ordinal);
        var whole = dot < 0 ? text : text[..dot];
        var fraction = dot < 0 ? string.Empty : text[(dot + 1)..];
        var all = (whole + fraction).TrimStart('0');
        var leading = (whole + fraction).Length - all.Length;
        var exponent = whole.Length - 1 - leading + e;

        return (all.TrimEnd('0') is { Length: > 0 } trimmed ? trimmed : "0", exponent);
    }

    public static JsonNode? Int(string text) => Number(ParseInt(text));

    public static JsonNode? Float(string text) => Number(ParseFloat(text));
}
