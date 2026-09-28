using System;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// The shape of a node, a meta key and an assignment, and how an assignment matches a node.
/// A node is lowercase dotted segments of <c>a-z</c>, <c>0-9</c> and <c>_</c>
/// (<c>room.enter.locked</c>). An assignment is a node, or a node prefix ending in <c>.*</c>
/// (<c>room.enter.*</c>), or <c>*</c> alone.
/// </summary>
public static class PermissionNodeFormat
{
    /// <summary>
    /// The longest node or meta key, in characters. Fixed by the width of the <c>node</c> and
    /// <c>key</c> columns, not a tunable.
    /// </summary>
    public const int MAX_LENGTH = 128;

    public const string WILDCARD = "*";

    private const string WILDCARD_SUFFIX = ".*";

    /// <summary>What <see cref="Specificity"/> returns for an exact match.</summary>
    public const int EXACT = int.MaxValue;

    /// <summary>What <see cref="Specificity"/> returns when the assignment does not match.</summary>
    public const int NO_MATCH = -1;

    /// <summary>Whether <paramref name="value"/> is a concrete node or meta key.</summary>
    public static bool IsValidNode(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MAX_LENGTH)
            return false;

        foreach (var segment in value.Split('.'))
        {
            if (!IsValidSegment(segment))
                return false;
        }

        return true;
    }

    /// <summary>Whether <paramref name="value"/> is a single segment: a plugin prefix, say.</summary>
    public static bool IsValidSegment(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (var c in value)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
                return false;
        }

        return true;
    }

    /// <summary>Whether <paramref name="value"/> may be stored as an assignment.</summary>
    public static bool IsValidAssignment(string? value) =>
        value == WILDCARD
        || IsValidNode(value)
        || (
            value is not null
            && value.Length <= MAX_LENGTH
            && value.EndsWith(WILDCARD_SUFFIX, StringComparison.Ordinal)
            && IsValidNode(value[..^WILDCARD_SUFFIX.Length])
        );

    public static bool IsWildcard(string assignment) =>
        assignment == WILDCARD || assignment.EndsWith(WILDCARD_SUFFIX, StringComparison.Ordinal);

    /// <summary>
    /// How specifically <paramref name="assignment"/> names <paramref name="node"/>:
    /// <see cref="EXACT"/> for the node itself, the length of the prefix for a wildcard that
    /// covers it (so <c>room.enter.*</c> outranks <c>room.*</c>, which outranks <c>*</c> at 0),
    /// and <see cref="NO_MATCH"/> otherwise. A wildcard covers the nodes under its prefix, not
    /// the prefix itself: <c>room.*</c> does not match <c>room</c>.
    /// </summary>
    public static int Specificity(string assignment, string node)
    {
        if (string.Equals(assignment, node, StringComparison.Ordinal))
            return EXACT;

        if (assignment == WILDCARD)
            return 0;

        if (!assignment.EndsWith(WILDCARD_SUFFIX, StringComparison.Ordinal))
            return NO_MATCH;

        // Keep the dot, so room.* covers room.enter but not roomy.enter.
        var prefix = assignment.AsSpan(0, assignment.Length - 1);

        return node.AsSpan().StartsWith(prefix, StringComparison.Ordinal)
            ? prefix.Length
            : NO_MATCH;
    }

    /// <summary>The first segment of a node: <c>room</c> for <c>room.enter.locked</c>.</summary>
    public static string RootOf(string node)
    {
        var dot = node.IndexOf('.', StringComparison.Ordinal);

        return dot < 0 ? node : node[..dot];
    }
}
