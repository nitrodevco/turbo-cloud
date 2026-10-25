namespace Turbo.Primitives.Plugins.Messages.Enums;

/// <summary>
/// What a plugin message field carries, and how it is written: Habbo's wire encoding for each
/// scalar, an array as an int count and its items, a record as its fields in order, a dictionary as an int
/// count and its keys and values.
/// </summary>
public enum PluginMessageValueKind
{
    /// <summary>A 32-bit int; an enum is sent as its int value.</summary>
    Int,

    /// <summary>A 64-bit int, 8 bytes big-endian.</summary>
    Long,

    /// <summary>One byte, 1 or 0.</summary>
    Bool,

    /// <summary>A UTF-8 string after its 16-bit length.</summary>
    String,

    /// <summary>An IEEE 754 double, 8 bytes big-endian.</summary>
    Double,

    /// <summary>An int count, then each item.</summary>
    Array,

    /// <summary>A record's fields, in order, with nothing around them.</summary>
    Object,

    /// <summary>A string-keyed dictionary: an int count, then each key (a string) and its value.</summary>
    Map,
}
