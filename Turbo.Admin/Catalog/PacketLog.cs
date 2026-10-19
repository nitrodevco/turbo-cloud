using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Turbo.Admin.Catalog;

/// <summary>
/// Reads a G-Earth packet log, where each packet is a <c>[Name]</c> line, then its bytes as
/// <c>Incoming[header] -&gt; ...</c> (<c>Outgoing</c> for what the client sent), then the same
/// packet as an expression, <c>{in:Name}{b:true}{i:1}{s:"text"}</c>.
/// <list type="bullet">
/// <item>The bytes are the packet whole, its length and header first. G-Earth writes a byte below
/// 32, 127 and the four brackets and braces as <c>[n]</c> and every other byte as the character
/// of that code, so they are read exactly, and in preference to the expression.</item>
/// <item>A packet whose bytes do not come to the length they start with (a line cut short, or
/// its trailing spaces trimmed) is read from its expression when the log has one, and reported
/// otherwise.</item>
/// <item>A packet given only as an expression is read from it; its header is 0 unless the
/// expression names it by number (<c>{in:1171}</c>, or <c>{h:1171}</c>).</item>
/// </list>
/// Header ids change with every client release, so a reader tells packets apart by name.
/// </summary>
public static partial class PacketLog
{
    private const int LENGTH_BYTES = 4;
    private const int HEADER_BYTES = 2;
    private const string INCOMING = "Incoming";
    private const string EXPRESSION_IN = "in";
    private const string EXPRESSION_OUT = "out";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    /// A log file's text: UTF-8 as G-Earth saves it, or, when it is not UTF-8, each byte the
    /// character of its code, as an editor saving it in a single-byte code page leaves it.
    /// </summary>
    public static string TextOf(byte[] file)
    {
        string text;

        try
        {
            text = StrictUtf8.GetString(file);
        }
        catch (DecoderFallbackException)
        {
            text = Encoding.Latin1.GetString(file);
        }

        return text.TrimStart('﻿');
    }

    /// <summary>The packets of the log, in its order, those that could not be read with why.</summary>
    public static IReadOnlyList<LoggedPacket> Read(string text)
    {
        var packets = new List<LoggedPacket>();
        string? name = null;
        Pending? current = null;
        var lineNumber = 0;

        using var reader = new StringReader(text);

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;

            if (BytesLine().Match(line) is { Success: true } bytesLine)
            {
                Flush();

                current = new Pending(
                    lineNumber,
                    name,
                    bytesLine.Groups["direction"].Value == INCOMING
                );
                name = null;

                var stated = int.Parse(
                    bytesLine.Groups["header"].Value,
                    CultureInfo.InvariantCulture
                );

                if (
                    Packet(bytesLine.Groups["bytes"].Value, out var header, out var payload) is
                    { } problem
                )
                    current.Problem = problem;
                else if (header != stated)
                    current.Problem =
                        $"its bytes give header {header}, not the {stated} it is logged as";
                else
                {
                    current.Header = header;
                    current.Payload = payload;
                }

                continue;
            }

            var trimmed = line.Trim();

            if (
                trimmed.StartsWith($"{{{EXPRESSION_IN}:", StringComparison.Ordinal)
                || trimmed.StartsWith($"{{{EXPRESSION_OUT}:", StringComparison.Ordinal)
            )
            {
                var expression = Expression(trimmed);

                // The expression of the packet whose bytes came just before it says what they
                // say; it names the packet, and stands in for bytes that could not be read.
                if (current is { HasExpression: false } && current.Incoming == expression.Incoming)
                {
                    current.HasExpression = true;
                    current.Name ??= expression.Name;

                    if (current.Payload is null && expression.Payload is not null)
                    {
                        current.Header = expression.Header;
                        current.Payload = expression.Payload;
                    }
                    else if (current.Payload is null)
                        current.Problem = $"{current.Problem}, and {expression.Problem}";

                    continue;
                }

                Flush();

                current = new Pending(lineNumber, name ?? expression.Name, expression.Incoming)
                {
                    HasExpression = true,
                    Header = expression.Header,
                    Payload = expression.Payload,
                    Problem = expression.Problem,
                };
                name = null;

                continue;
            }

            if (NameLine().Match(line) is { Success: true } nameLine)
            {
                Flush();
                name = nameLine.Groups["name"].Value;
            }
        }

        Flush();

        return packets;

        void Flush()
        {
            if (current is null)
                return;

            packets.Add(
                new LoggedPacket(
                    current.Name,
                    current.Incoming,
                    current.Header,
                    current.Payload,
                    current.Payload is null ? current.Problem ?? "it holds no packet" : null,
                    current.Line
                )
            );

            current = null;
        }
    }

    /// <summary>A packet's bytes as G-Earth writes them, checked against the length they start with.</summary>
    private static string? Packet(string text, out int header, out byte[] payload)
    {
        header = 0;
        payload = [];

        var bytes = new List<byte>(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '[' && EscapedByte(text, i, out var value, out var end))
            {
                bytes.Add(value);
                i = end;
            }
            else if (c <= byte.MaxValue)
                bytes.Add((byte)c);
            else
                return $"'{c}' at {i} is not a byte";
        }

        if (bytes.Count < LENGTH_BYTES + HEADER_BYTES)
            return "its bytes are too few to be a packet";

        var all = bytes.ToArray();
        var length = BinaryPrimitives.ReadInt32BigEndian(all);

        if (length != all.Length - LENGTH_BYTES)
            return $"its bytes come to {all.Length - LENGTH_BYTES} after its length, not the {length} it starts with";

        header = BinaryPrimitives.ReadUInt16BigEndian(all.AsSpan(LENGTH_BYTES));
        payload = all[(LENGTH_BYTES + HEADER_BYTES)..];

        return null;
    }

    /// <summary><c>[n]</c> at <paramref name="start"/>: a byte G-Earth wrote by its number.</summary>
    private static bool EscapedByte(string text, int start, out byte value, out int end)
    {
        value = 0;
        end = text.IndexOf(']', start + 1);

        return end > start + 1
            && end - start - 1 <= 3
            && byte.TryParse(
                text.AsSpan(start + 1, end - start - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value
            );
    }

    /// <summary>
    /// A packet's expression written out as the bytes it stands for: <c>{i:n}</c> four bytes,
    /// <c>{u:n}</c> two, <c>{l:n}</c> and <c>{d:n}</c> eight, <c>{b:true}</c> or <c>{b:n}</c>
    /// one, <c>{s:"text"}</c> its UTF-8 bytes after their count in two.
    /// </summary>
    private static ParsedExpression Expression(string text)
    {
        var incoming = text.StartsWith($"{{{EXPRESSION_IN}:", StringComparison.Ordinal);
        string? name = null;
        var header = 0;
        var payload = new MemoryStream();
        Span<byte> scratch = stackalloc byte[sizeof(long)];
        var at = 0;

        while (at < text.Length)
        {
            if (text[at] != '{')
                return Refused($"its expression has '{text[at]}' at {at} outside a value");

            var colon = text.IndexOf(':', at);

            if (colon < 0)
                return Refused("its expression ends inside a value");

            var type = text[(at + 1)..colon];
            string value;

            if (type == "s")
            {
                if (ReadQuoted(text, colon + 1, out value, out var close) is { } badString)
                    return Refused(badString);

                at = close + 1;
            }
            else
            {
                var close = text.IndexOf('}', colon);

                if (close < 0)
                    return Refused("its expression ends inside a value");

                value = text[(colon + 1)..close];
                at = close + 1;
            }

            switch (type)
            {
                case EXPRESSION_IN or EXPRESSION_OUT:
                    if (
                        int.TryParse(
                            value,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var numbered
                        )
                    )
                        header = numbered;
                    else
                        name = value;
                    break;
                case "h":
                    if (
                        !int.TryParse(
                            value,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out header
                        )
                    )
                        return Refused($"its header {value} is not a number");
                    break;
                case "i":
                    if (
                        !int.TryParse(
                            value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var integer
                        )
                    )
                        return Refused($"{{i:{value}}} is not a whole number");
                    BinaryPrimitives.WriteInt32BigEndian(scratch, integer);
                    payload.Write(scratch[..sizeof(int)]);
                    break;
                case "u":
                    if (
                        !int.TryParse(
                            value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var shortValue
                        )
                    )
                        return Refused($"{{u:{value}}} is not a whole number");
                    BinaryPrimitives.WriteUInt16BigEndian(scratch, unchecked((ushort)shortValue));
                    payload.Write(scratch[..sizeof(ushort)]);
                    break;
                case "l":
                    if (
                        !long.TryParse(
                            value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var longValue
                        )
                    )
                        return Refused($"{{l:{value}}} is not a whole number");
                    BinaryPrimitives.WriteInt64BigEndian(scratch, longValue);
                    payload.Write(scratch);
                    break;
                case "d":
                    if (
                        !double.TryParse(
                            value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var doubleValue
                        )
                    )
                        return Refused($"{{d:{value}}} is not a number");
                    BinaryPrimitives.WriteDoubleBigEndian(scratch, doubleValue);
                    payload.Write(scratch);
                    break;
                case "b":
                    if (bool.TryParse(value, out var flag))
                        payload.WriteByte(flag ? (byte)1 : (byte)0);
                    else if (
                        int.TryParse(
                            value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var byteValue
                        ) && byteValue is >= sbyte.MinValue and <= byte.MaxValue
                    )
                        payload.WriteByte(unchecked((byte)byteValue));
                    else
                        return Refused($"{{b:{value}}} is not a byte");
                    break;
                case "s":
                    var utf8 = Encoding.UTF8.GetBytes(value);

                    if (utf8.Length > ushort.MaxValue)
                        return Refused("a string of its expression is too long for a packet");

                    BinaryPrimitives.WriteUInt16BigEndian(scratch, (ushort)utf8.Length);
                    payload.Write(scratch[..sizeof(ushort)]);
                    payload.Write(utf8);
                    break;
                default:
                    return Refused($"its expression has a value of kind {type}, which is not read");
            }
        }

        return new ParsedExpression(incoming, name, header, payload.ToArray(), null);

        ParsedExpression Refused(string problem) => new(incoming, name, 0, null, problem);
    }

    /// <summary>A quoted string of an expression from <paramref name="start"/>, its escapes undone, to its closing brace.</summary>
    private static string? ReadQuoted(string text, int start, out string value, out int close)
    {
        value = string.Empty;
        close = -1;

        if (start >= text.Length || text[start] != '"')
            return "a string of its expression is not quoted";

        var builder = new StringBuilder();

        for (var i = start + 1; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '\\' && i + 1 < text.Length)
            {
                i++;
                builder.Append(
                    text[i] switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => text[i],
                    }
                );

                continue;
            }

            if (c != '"')
            {
                builder.Append(c);
                continue;
            }

            if (i + 1 >= text.Length || text[i + 1] != '}')
                return "a string of its expression does not end its value";

            value = builder.ToString();
            close = i + 1;

            return null;
        }

        return "a string of its expression is not closed";
    }

    [GeneratedRegex(
        @"^\s*(?<direction>Incoming|Outgoing)\[(?<header>\d+)\]\s*(->|<-)\s?(?<bytes>.*)$"
    )]
    private static partial Regex BytesLine();

    [GeneratedRegex(@"^\s*\[(?<name>[A-Za-z_][A-Za-z0-9_]*)\]\s*$")]
    private static partial Regex NameLine();

    /// <summary>A packet of the log as it is being read; its bytes may come from its expression.</summary>
    private sealed class Pending(int line, string? name, bool incoming)
    {
        public int Line { get; } = line;

        public string? Name { get; set; } = name;

        public bool Incoming { get; } = incoming;

        public int Header { get; set; }

        public byte[]? Payload { get; set; }

        public string? Problem { get; set; }

        public bool HasExpression { get; set; }
    }

    private sealed record ParsedExpression(
        bool Incoming,
        string? Name,
        int Header,
        byte[]? Payload,
        string? Problem
    );
}
