using System;
using System.Linq;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What changed in a file a target holds an older copy of (<c>AssetComparison</c>). <c>result</c> is
/// <c>changed</c>, <c>stamp-only</c> (only the client build stamp differs: nothing changes for
/// players), <c>same</c>, <c>not-on-target</c> or <c>unreadable</c> (<c>problem</c> says why).
/// <c>source</c>, <c>revision</c> and <c>updatedAt</c> are the hotel's bundle row: where its copy came
/// from and when it last changed; <c>stampBefore</c>/<c>stampAfter</c> the build stamp of the
/// target's copy and the hotel's.
/// </summary>
public sealed record AssetCompareItem(
    string Kind,
    string Name,
    string Path,
    long Size,
    string Result,
    string? Problem,
    long? TargetSize,
    bool TargetAsRecorded,
    string? Source,
    string? Revision,
    DateTime? UpdatedAt,
    string? StampBefore,
    string? StampAfter,
    AssetCompareWindow[] Windows,
    AssetCompareTexts[] Texts,
    AssetCompareEntry[] Entries,
    int EntriesChanged
)
{
    public static AssetCompareItem From(AssetPublishComparison compared) =>
        new(
            AssetContractNames.Of(compared.File.Kind),
            compared.File.Name,
            compared.File.Path,
            compared.File.Size,
            AssetCompareNames.Of(compared.Result),
            compared.Problem,
            compared.TargetBytes,
            compared.TargetAsRecorded,
            compared.Source is { } source ? AssetContractNames.Of(source) : null,
            compared.Revision,
            compared.UpdatedAt,
            compared.StampBefore,
            compared.StampAfter,
            [.. compared.Windows.Select(AssetCompareWindow.From)],
            [.. compared.Texts.Select(AssetCompareTexts.From)],
            [.. compared.Entries.Select(AssetCompareEntry.From)],
            compared.EntriesChanged
        );
}

/// <summary>A window template added, removed or changed, with its elements and text keys that changed.</summary>
public sealed record AssetCompareWindow(
    string Id,
    string Change,
    string[] ElementsAdded,
    string[] ElementsRemoved,
    string[] ElementsChanged,
    string[] TextKeysAdded,
    string[] TextKeysRemoved
)
{
    public static AssetCompareWindow From(AssetWindowDiff diff) =>
        new(
            diff.Id,
            AssetCompareNames.Of(diff.Change),
            [.. diff.ElementsAdded],
            [.. diff.ElementsRemoved],
            [.. diff.ElementsChanged],
            [.. diff.TextKeysAdded],
            [.. diff.TextKeysRemoved]
        );
}

/// <summary>One language's text keys added, removed and changed, and the first of them with their texts.</summary>
public sealed record AssetCompareTexts(
    string Language,
    int Added,
    int Removed,
    int Changed,
    AssetCompareText[] Edits
)
{
    public static AssetCompareTexts From(AssetTextDiff diff) =>
        new(
            diff.Language,
            diff.Added,
            diff.Removed,
            diff.Changed,
            [
                .. diff.Edits.Select(x => new AssetCompareText(
                    x.Key,
                    AssetCompareNames.Of(x.Change),
                    x.Before,
                    x.After
                )),
            ]
        );
}

public sealed record AssetCompareText(string Key, string Change, string? Before, string? After);

/// <summary>Another file that changed (an image, the asset data), with its bytes and, for an image, its pixels on each side.</summary>
public sealed record AssetCompareEntry(
    string Name,
    string Change,
    long? BytesBefore,
    long? BytesAfter,
    int? WidthBefore,
    int? HeightBefore,
    int? WidthAfter,
    int? HeightAfter
)
{
    public static AssetCompareEntry From(AssetEntryDiff diff) =>
        new(
            diff.Name,
            AssetCompareNames.Of(diff.Change),
            diff.BytesBefore,
            diff.BytesAfter,
            diff.PixelsBefore?.Width,
            diff.PixelsBefore?.Height,
            diff.PixelsAfter?.Width,
            diff.PixelsAfter?.Height
        );
}

/// <summary>The comparisons asked for; <c>total</c> counts the changed files there are when every one was asked.</summary>
public sealed record AssetCompareResponse(AssetCompareItem[] Items, int Total)
{
    public static AssetCompareResponse From(AssetPublishComparisons comparisons) =>
        new([.. comparisons.Items.Select(AssetCompareItem.From)], comparisons.Total);
}

public static class AssetCompareNames
{
    public static string Of(AssetCompareResult result) =>
        result switch
        {
            AssetCompareResult.Changed => "changed",
            AssetCompareResult.StampOnly => "stamp-only",
            AssetCompareResult.Same => "same",
            AssetCompareResult.NotOnTarget => "not-on-target",
            AssetCompareResult.Unreadable => "unreadable",
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
        };

    public static string Of(AssetDiffChange change) =>
        change switch
        {
            AssetDiffChange.Added => "added",
            AssetDiffChange.Removed => "removed",
            AssetDiffChange.Changed => "changed",
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, null),
        };
}
