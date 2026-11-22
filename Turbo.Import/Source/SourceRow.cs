using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;

namespace Turbo.Import.Source;

/// <summary>
/// The row a <see cref="SourceDatabase"/> read is on. Every getter takes the column by name and
/// answers its fallback when the column is not there or holds NULL, so one importer reads every
/// version of a schema: a column a hotel never added, or one an older version named otherwise,
/// is a default, not a failure. Values are read leniently, because the same flag is an
/// <c>ENUM('0','1')</c> in one table, a <c>TINYINT(1)</c> in another and text in a third.
/// <para>
/// It is a view of the reader's current row: read what you need before the next row.
/// </para>
/// </summary>
internal sealed class SourceRow
{
    private readonly DbDataReader? _reader;
    private readonly Dictionary<string, int> _ordinals;
    private readonly object?[]? _values;

    public SourceRow(DbDataReader reader)
    {
        _reader = reader;
        _ordinals = new Dictionary<string, int>(
            reader.FieldCount,
            StringComparer.OrdinalIgnoreCase
        );

        for (var i = 0; i < reader.FieldCount; i++)
            _ordinals.TryAdd(reader.GetName(i), i);
    }

    private SourceRow(Dictionary<string, int> ordinals, object?[] values)
    {
        _ordinals = ordinals;
        _values = values;
    }

    /// <summary>A copy of the current row that outlives the reader: for the few rows kept.</summary>
    public SourceRow Detach()
    {
        var values = new object?[_ordinals.Count == 0 ? 0 : _ordinals.Values.Max() + 1];

        foreach (var (column, i) in _ordinals)
            values[i] = Value(column);

        return new SourceRow(_ordinals, values);
    }

    /// <summary>The raw value, or null for a missing column, NULL or a zero date.</summary>
    public object? Value(string column)
    {
        if (!_ordinals.TryGetValue(column, out var i))
            return null;

        if (_values is not null)
            return _values[i];

        if (_reader!.IsDBNull(i))
            return null;

        try
        {
            return _reader.GetValue(i);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException)
        {
            // A MySQL zero date ('0000-00-00') or a value the driver cannot represent.
            return null;
        }
    }

    public string? String(string column) =>
        Value(column) switch
        {
            null => null,
            string s => s,
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            var v => v.ToString(),
        };

    public string Text(string column, string fallback = "") => String(column) ?? fallback;

    public long Long(string column, long fallback = 0) =>
        Value(column) switch
        {
            null => fallback,
            bool b => b ? 1 : 0,
            long l => l,
            int i => i,
            short s => s,
            sbyte sb => sb,
            byte by => by,
            ushort us => us,
            uint ui => ui,
            ulong ul => ul > long.MaxValue ? long.MaxValue : (long)ul,
            decimal d => (long)Math.Round(d),
            double db => double.IsFinite(db) ? (long)Math.Round(db) : fallback,
            float f => float.IsFinite(f) ? (long)Math.Round(f) : fallback,
            string s => ParseLong(s, fallback),
            var v => ParseLong(Convert.ToString(v, CultureInfo.InvariantCulture), fallback),
        };

    public int Int(string column, int fallback = 0) => Clamp(Long(column, fallback));

    public double Double(string column, double fallback = 0) =>
        Value(column) switch
        {
            null => fallback,
            bool b => b ? 1 : 0,
            double d => d,
            float f => f,
            decimal m => (double)m,
            string s => double.TryParse(
                s.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed
            )
                ? parsed
                : fallback,
            var v => Convert.ToDouble(v, CultureInfo.InvariantCulture),
        };

    /// <summary>
    /// A flag: <c>'1'</c>, <c>1</c>, <c>true</c>, <c>'yes'</c> and <c>'true'</c> are set; anything
    /// else that is there is not.
    /// </summary>
    public bool Bool(string column, bool fallback = false) =>
        Value(column) switch
        {
            null => fallback,
            bool b => b,
            string s => s.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "y",
            var v => Long(column, 0) != 0 || v is true,
        };

    /// <summary>A Unix time in seconds, as UTC; null for none (0 or below).</summary>
    public DateTime? UnixTime(string column)
    {
        var value = Value(column);

        if (value is DateTime dt)
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        var seconds = Long(column, 0);

        return SourceTime.FromUnix(seconds);
    }

    private static long ParseLong(string? s, long fallback)
    {
        if (string.IsNullOrWhiteSpace(s))
            return fallback;

        var trimmed = s.Trim();

        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
            return l;

        return
            double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            && double.IsFinite(d)
            ? (long)Math.Round(d)
            : fallback;
    }

    private static int Clamp(long value) =>
        value > int.MaxValue ? int.MaxValue
        : value < int.MinValue ? int.MinValue
        : (int)value;
}

/// <summary>Morningstar's times: Unix seconds, read as UTC.</summary>
internal static class SourceTime
{
    // Past this a "timestamp" is not one (Morningstar writes 2147483647 for "never expires").
    private static readonly DateTime LATEST = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static DateTime? FromUnix(long seconds)
    {
        if (seconds <= 0)
            return null;

        var time = DateTimeOffset.FromUnixTimeSeconds(Math.Min(seconds, 4102444800L)).UtcDateTime;

        return time >= LATEST ? null : time;
    }
}
