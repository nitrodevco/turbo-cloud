using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Turbo.Primitives.Rooms.Snapshots.Wired.Leaderboard;

namespace Turbo.Rooms.Wired.Leaderboard;

/// <summary>
/// A Leaderboard add-on's saved settings, read the way the client writes them
/// (<c>wired_setup/addons/leaderboard/VariableLeaderboardConfigCodec.as</c>, unchanged in
/// WIN63-202610131318).
/// <para>
/// 19 ints: [0] sort mode, [1] column count, [2..5] column types (-1 unused), [6] filter on,
/// [7] comparison on, [8] operand kind (0 literal, 1 global variable), [9] display name count,
/// [10..13] tile x, tile y, x offset, altitude, [14..17] column widths, [18] page size option.
/// 12 variable ids: [0] filter variable, [1] global operand, [2..11] the variables with a display
/// name. One string of 17 tab-separated fields: title, description, 4 column names, the literal,
/// 10 display names. An empty string is a box that was never published.
/// </para>
/// </summary>
public sealed record VariableLeaderboardConfig
{
    public const int INT_PARAM_COUNT = 19;
    public const int VARIABLE_ID_COUNT = 12;
    public const int MAX_COLUMNS = 4;
    public const int MAX_DISPLAY_OVERRIDES = 10;
    public const int MAX_TITLE_LENGTH = 50;
    public const int MAX_DESCRIPTION_LENGTH = 80;
    public const int MIN_COLUMN_LABEL_LENGTH = 2;
    public const int MAX_COLUMN_LABEL_LENGTH = 12;
    public const int MAX_OVERRIDE_LENGTH = 22;
    public const int MAX_OFFSET_X = 32;
    public const int COLUMN_USERNAME = 0;
    public const int COLUMN_VALUE = 1;
    public const int COLUMN_LAST_UPDATE = 2;
    public const int COLUMN_CREATION = 3;
    public const int COLUMN_LEVEL = 4;
    public const int COLUMN_TEXT = 5;
    public const int SORT_HIGHEST_VALUE = 0;
    public const int SORT_LOWEST_VALUE = 1;
    public const int SORT_OLDEST_CREATION = 2;
    public const int SORT_LATEST_CREATION = 3;
    public const int SORT_OLDEST_UPDATE = 4;
    public const int SORT_LATEST_UPDATE = 5;
    public const int OPERAND_LITERAL = 0;
    public const int OPERAND_GLOBAL = 1;
    public const string NULL_VARIABLE_ID = "0";

    // Storage limits the client checks before it sends (validateStorageLengths).
    private const int MAX_INT_PARAMS_STORAGE_LENGTH = 400;
    private const int MAX_VARIABLE_IDS_STORAGE_LENGTH = 240;
    private const int MAX_STRING_PARAM_UTF8_LENGTH = 1000;

    /// <summary>Rows per page by page size option (<c>VariableLeaderboardDescriptorData</c>).</summary>
    public static readonly ImmutableArray<int> PageSizes = [10, 15, 20];

    /// <summary>
    /// The ints of a box that was never published: the codec's <c>decodeUnpublished</c> (highest
    /// value, Username XL + Value small, no filter, location 0,0, page size 10) as <c>encode</c>
    /// writes it. Habbo sends them as a fresh box's ints and defaults (official client capture,
    /// WIN63-202610151745).
    /// </summary>
    public static readonly ImmutableArray<int> UnpublishedIntParams =
    [
        SORT_HIGHEST_VALUE,
        2,
        COLUMN_USERNAME,
        COLUMN_VALUE,
        -1,
        -1,
        0,
        0,
        OPERAND_LITERAL,
        0,
        0,
        0,
        0,
        0,
        3,
        0,
        0,
        0,
        0,
    ];

    public required string Title { get; init; }
    public required string Description { get; init; }
    public required ImmutableArray<VariableLeaderboardColumnSnapshot> Columns { get; init; }
    public required int SortMode { get; init; }
    public required bool FilterEnabled { get; init; }
    public required string FilterVariableId { get; init; }
    public required bool ComparisonEnabled { get; init; }
    public required int OperandKind { get; init; }
    public required string GlobalOperandVariableId { get; init; }
    public required string Literal { get; init; }

    /// <summary>Display names by variable id, in the order they were saved.</summary>
    public required ImmutableArray<KeyValuePair<string, string>> DisplayOverrides { get; init; }

    public required int TileX { get; init; }
    public required int TileY { get; init; }
    public required int OffsetX { get; init; }
    public required int Altitude { get; init; }
    public required int PageSizeOption { get; init; }

    /// <summary>
    /// The settings a save carries, or null with the reason when the client would not have sent
    /// them (<c>VariableLeaderboardConfigCodec.decodeParams</c> + <c>validate</c>). An empty
    /// string decodes to null with no reason: an unpublished box.
    /// </summary>
    public static VariableLeaderboardConfig? Decode(
        IReadOnlyList<int> intParams,
        IReadOnlyList<string> variableIds,
        string? stringParam,
        out string? invalidReason
    )
    {
        invalidReason = null;

        if (string.IsNullOrEmpty(stringParam))
            return null;

        try
        {
            return DecodePublished(intParams, variableIds, stringParam);
        }
        catch (FormatException ex)
        {
            invalidReason = ex.Message;

            return null;
        }
    }

    private static VariableLeaderboardConfig DecodePublished(
        IReadOnlyList<int> ints,
        IReadOnlyList<string> ids,
        string stringParam
    )
    {
        if (string.Join("|", ints).Length > MAX_INT_PARAMS_STORAGE_LENGTH)
            throw Invalid("integer parameters exceed storage limit");

        if (string.Join("|", ids).Length > MAX_VARIABLE_IDS_STORAGE_LENGTH)
            throw Invalid("variable IDs exceed storage limit");

        if (Encoding.UTF8.GetByteCount(stringParam) > MAX_STRING_PARAM_UTF8_LENGTH)
            throw Invalid("string parameter exceeds storage limit");

        if (ints.Count < 14)
            throw Invalid("expected at least 14 integer parameters");

        if (ids.Count < VARIABLE_ID_COUNT)
            throw Invalid("expected at least 12 variable IDs");

        var strings = stringParam.Split('\t');
        string Field(int index) => index < strings.Length ? strings[index] : string.Empty;

        var sortMode = ints[0];

        if (sortMode is < SORT_HIGHEST_VALUE or > SORT_LATEST_UPDATE)
            throw Invalid("unknown sort mode");

        var columnCount = ints[1];

        if (columnCount is < 1 or > MAX_COLUMNS)
            throw Invalid("invalid column count");

        var columns = ImmutableArray.CreateBuilder<VariableLeaderboardColumnSnapshot>();
        var seen = new HashSet<int>();

        for (var i = 0; i < columnCount; i++)
        {
            var type = ints[2 + i];

            if (type is < COLUMN_USERNAME or > COLUMN_TEXT)
                throw Invalid("unknown or unused column type");

            if (i == 0 && type != COLUMN_USERNAME)
                throw Invalid("first column must be Username");

            if (!seen.Add(type))
                throw Invalid("duplicate column type");

            var label = Field(2 + i);

            if (label.Length is < MIN_COLUMN_LABEL_LENGTH or > MAX_COLUMN_LABEL_LENGTH)
                throw Invalid("invalid column label length");

            var width = 14 + i < ints.Count ? ints[14 + i] : DefaultWidth(type);

            if (width is < 0 or > 3)
                width = DefaultWidth(type);

            columns.Add(
                new VariableLeaderboardColumnSnapshot
                {
                    Type = type,
                    Label = label,
                    Width = width,
                }
            );
        }

        var filterEnabled = Bool(ints[6]);
        var comparisonEnabled = filterEnabled && Bool(ints[7]);
        var operandKind = comparisonEnabled ? ints[8] : OPERAND_LITERAL;

        if (operandKind is not (OPERAND_LITERAL or OPERAND_GLOBAL))
            throw Invalid("unknown operand kind");

        var filterVariableId = filterEnabled ? Canonical(ids[0]) : NULL_VARIABLE_ID;

        if (filterEnabled && filterVariableId == NULL_VARIABLE_ID)
            throw Invalid("enabled filter requires a variable ID");

        var globalOperandVariableId = NULL_VARIABLE_ID;
        var literal = string.Empty;

        if (comparisonEnabled)
        {
            if (operandKind == OPERAND_LITERAL)
            {
                literal = Field(6);

                if (
                    !long.TryParse(
                        literal,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out var parsed
                    )
                    || parsed.ToString(CultureInfo.InvariantCulture) != literal
                )
                    throw Invalid(
                        "literal comparison must contain a canonical signed 64-bit literal"
                    );
            }
            else
            {
                globalOperandVariableId = Canonical(ids[1]);

                if (globalOperandVariableId == NULL_VARIABLE_ID)
                    throw Invalid("global comparison requires a variable ID");
            }
        }

        var overrideCount = ints[9];

        if (overrideCount is < 0 or > MAX_DISPLAY_OVERRIDES)
            throw Invalid("invalid display override count");

        var overrides = ImmutableArray.CreateBuilder<KeyValuePair<string, string>>();
        var overrideIds = new HashSet<string>();

        for (var i = 0; i < overrideCount; i++)
        {
            var variableId = ids[2 + i];
            var name = Field(7 + i);

            if (Canonical(variableId) == NULL_VARIABLE_ID || name.Length == 0)
                throw Invalid("populated display override must have an ID and name");

            if (name.Length > MAX_OVERRIDE_LENGTH)
                throw Invalid("invalid display override length");

            if (!overrideIds.Add(variableId))
                throw Invalid("duplicate display override ID");

            overrides.Add(new(variableId, name));
        }

        var title = Field(0);
        var description = Field(1);

        if (title.Length is < 1 or > MAX_TITLE_LENGTH)
            throw Invalid("invalid title length");

        if (description.Length > MAX_DESCRIPTION_LENGTH)
            throw Invalid("invalid description length");

        var offsetX = ints[12];

        if (offsetX is < -MAX_OFFSET_X or > MAX_OFFSET_X)
            throw Invalid("offsetX must be between -32 and 32");

        var pageSizeOption = ints.Count > 18 ? ints[18] : 0;

        if (pageSizeOption < 0 || pageSizeOption >= PageSizes.Length)
            throw Invalid("invalid page size option");

        return new VariableLeaderboardConfig
        {
            Title = title,
            Description = description,
            Columns = columns.ToImmutable(),
            SortMode = sortMode,
            FilterEnabled = filterEnabled,
            FilterVariableId = filterVariableId,
            ComparisonEnabled = comparisonEnabled,
            OperandKind = operandKind,
            GlobalOperandVariableId = globalOperandVariableId,
            Literal = literal,
            DisplayOverrides = overrides.ToImmutable(),
            TileX = ints[10],
            TileY = ints[11],
            OffsetX = offsetX,
            Altitude = ints[13],
            PageSizeOption = pageSizeOption,
        };
    }

    public bool HasColumn(int type) => Columns.Any(column => column.Type == type);

    /// <summary>The display name saved for a variable, or null.</summary>
    public string? DisplayOverrideFor(string variableId)
    {
        foreach (var (id, name) in DisplayOverrides)
        {
            if (id == variableId)
                return name;
        }

        return null;
    }

    /// <summary>
    /// The name a variable gets when none was saved for it
    /// (<c>VariableLeaderboardDisplayNameFormatter.suggest</c>): "_" and "-" become spaces and each
    /// word is capitalised; "Variable" for an empty name.
    /// </summary>
    public static string SuggestDisplayName(string? technicalName)
    {
        if (string.IsNullOrEmpty(technicalName))
            return "Variable";

        var words = technicalName
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word =>
            {
                var lower = word.ToLowerInvariant();

                return char.ToUpperInvariant(lower[0]) + lower[1..];
            })
            .ToList();

        return words.Count == 0 ? "Variable" : string.Join(' ', words);
    }

    /// <summary>A column's width when none was saved (<c>_-53.defaultWidth</c>).</summary>
    public static int DefaultWidth(int type) =>
        type switch
        {
            COLUMN_USERNAME => 3,
            COLUMN_LAST_UPDATE or COLUMN_CREATION => 1,
            COLUMN_TEXT => 2,
            _ => 0,
        };

    private static bool Bool(int value) =>
        value switch
        {
            0 => false,
            1 => true,
            _ => throw Invalid("a flag must be zero or one"),
        };

    private static string Canonical(string? variableId) =>
        string.IsNullOrEmpty(variableId) || variableId is NULL_VARIABLE_ID or "n"
            ? NULL_VARIABLE_ID
            : variableId;

    private static FormatException Invalid(string reason) =>
        new($"Invalid variable leaderboard configuration: {reason}");
}
