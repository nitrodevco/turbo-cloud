using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Turbo.Achievements;

/// <summary>What syncing a file with the hotel's catalog did, or would do on a dry run.</summary>
/// <param name="Created">Keys in the file the hotel does not have yet.</param>
/// <param name="Revised">Keys whose definition differs, with the revision number they were given.</param>
/// <param name="Unchanged">Keys that already match the file exactly.</param>
/// <param name="NotInFile">
/// Keys the hotel has that the file does not mention. They are only reported: a missing entry
/// never retires anything, because retiring is a deliberate act (<c>achievement retire</c>).
/// </param>
/// <param name="Problems">
/// Everything that is wrong, all at once. Nothing is applied while there is any problem.
/// </param>
/// <param name="MissingTexts">Ready-to-paste <c>key=value</c> lines for texts the client will look for and not find.</param>
/// <param name="MissingBadgeImages">Badge image file names the achievements need and the badge directory lacks.</param>
/// <param name="Applied">True only when the changes were published.</param>
public sealed record AchievementSyncReport(
    ImmutableArray<string> Created,
    ImmutableArray<(string Key, int Revision)> Revised,
    ImmutableArray<string> Unchanged,
    ImmutableArray<string> NotInFile,
    ImmutableArray<string> Problems,
    ImmutableArray<string> MissingTexts,
    ImmutableArray<string> MissingBadgeImages,
    bool Applied
)
{
    public bool HasProblems => !Problems.IsDefaultOrEmpty && Problems.Length > 0;

    public bool ChangesCatalog => Created.Length > 0 || Revised.Length > 0;

    /// <summary>The report as console lines.</summary>
    public IEnumerable<string> ToLines(bool apply)
    {
        yield return $"Create {Created.Length}, revise {Revised.Length}, unchanged {Unchanged.Length}.";
        foreach (var key in Created)
            yield return $"  + {key}";
        foreach (var (key, revision) in Revised)
            yield return $"  ~ {key} -> revision {revision}";
        if (NotInFile.Length > 0)
            yield return $"Not in the file (left as they are; use 'achievement retire <key>'): {string.Join(", ", NotInFile)}";
        foreach (var problem in Problems)
            yield return $"PROBLEM: {problem}";
        if (MissingTexts.Length > 0)
        {
            yield return "Add these to the hotel's ExternalTexts (the client reads them):";
            foreach (var line in MissingTexts)
                yield return "  " + line;
        }
        if (MissingBadgeImages.Length > 0)
            yield return $"Badge images still needed in the badge directory: {string.Join(", ", MissingBadgeImages)}";
        yield return HasProblems ? "Nothing applied: fix the problems above."
        : Applied ? "Published."
        : !ChangesCatalog ? "Nothing to do."
        : apply ? "Nothing applied."
        : "Dry run: nothing published.";
    }
}
