using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Achievements.Configuration;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Texts;

namespace Turbo.Achievements;

/// <summary>
/// Makes the hotel's catalog match a file, so an owner edits the definitions they want and never
/// numbers a revision. A new key is created, one that differs gets the next revision, one that
/// already matches is left alone, and one the file leaves out is reported but never touched. The
/// whole file goes through the catalog's own validated, audited import as one atomic batch; if any
/// definition is wrong nothing is applied and every problem is listed.
/// </summary>
public sealed class AchievementSync(
    IAchievementCatalog catalog,
    IHotelTextProvider texts,
    IOptions<AchievementConfig> config,
    AchievementBadgeAssets? badgeAssets = null
)
{
    private readonly AchievementBadgeAssets _badgeAssets =
        badgeAssets ?? new AchievementBadgeAssets(config);

    /// <summary>A dry run is never recorded, so every validation-only import can share one id.</summary>
    private const string VALIDATION_OPERATION = "sync-check";

    public async Task<AchievementSyncReport> SyncAsync(
        ImmutableArray<AchievementDefinition> file,
        bool apply,
        string actor,
        string reason,
        string operationId,
        CancellationToken ct
    )
    {
        var problems = new List<string>();
        var create = new List<AchievementDefinition>();
        var revise = new List<AchievementDefinition>();
        var unchanged = new List<string>();
        var current = catalog.Current;
        foreach (
            var duplicate in file.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
        )
            problems.Add($"The key {duplicate.Key} appears {duplicate.Count()} times in the file.");
        foreach (var definition in file.DistinctBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var existing = current.FirstOrDefault(x =>
                string.Equals(x.Key, definition.Key, StringComparison.OrdinalIgnoreCase)
            );
            if (existing is null)
                create.Add(definition with { Revision = Math.Max(1, definition.Revision) });
            else if (existing.Id != definition.Id)
                problems.Add(
                    $"{definition.Key} has id {existing.Id} in the hotel but {definition.Id} in the file; an id never changes."
                );
            else if (Same(existing, definition))
                unchanged.Add(existing.Key);
            else
                revise.Add(definition with { Revision = existing.Revision + 1 });
        }
        var notInFile = current
            .Where(x =>
                !file.Any(f => string.Equals(f.Key, x.Key, StringComparison.OrdinalIgnoreCase))
            )
            .Select(x => x.Key)
            .ToImmutableArray();
        var changes = ImmutableArray.CreateRange(create.Concat(revise));
        problems.AddRange(await ValidateAsync(changes, ct).ConfigureAwait(false));
        await _badgeAssets
            .CheckAsync(
                changes
                    .Where(x => x.State != AchievementState.Disabled)
                    .SelectMany(x => x.Levels)
                    .Select(x => x.BadgeCode),
                ct
            )
            .ConfigureAwait(false);
        var shown = changes.Where(x => x.State != AchievementState.Disabled).ToArray();
        var known = await texts.GetTextsAsync(TextKeys(shown), ct).ConfigureAwait(false);
        var (missingTexts, missingImages) = Needs(shown, known);
        var applied = false;
        if (apply && problems.Count == 0 && changes.Length > 0)
        {
            await catalog
                .ImportAsync(changes, true, actor, reason, operationId, ct)
                .ConfigureAwait(false);
            applied = true;
        }

        return new(
            [.. create.Select(x => x.Key)],
            [.. revise.Select(x => (x.Key, x.Revision))],
            [.. unchanged],
            notInFile,
            [.. problems],
            missingTexts,
            missingImages,
            applied
        );
    }

    /// <summary>
    /// The catalog throws at the first thing wrong with a batch. Checking each changed definition
    /// on its own first lets a file's every problem be listed at once; the whole batch is then
    /// checked together for what only shows up in combination, such as a repeated badge code.
    /// </summary>
    private async Task<List<string>> ValidateAsync(
        ImmutableArray<AchievementDefinition> changes,
        CancellationToken ct
    )
    {
        var problems = new List<string>();
        if (changes.Length == 0)
            return problems;
        foreach (var definition in changes)
            try
            {
                await catalog
                    .ImportAsync([definition], false, "sync", "Validate", VALIDATION_OPERATION, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                problems.Add($"{definition.Key}: {ex.Message}");
            }
        if (problems.Count > 0)
            return problems;
        try
        {
            await catalog
                .ImportAsync(changes, false, "sync", "Validate", VALIDATION_OPERATION, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            problems.Add($"Together the changes are not valid: {ex.Message}");
        }

        return problems;
    }

    /// <summary>
    /// The texts the client will look for and the badge images it will draw, for every achievement
    /// players can be shown. An enabled one is also refused by the import until they exist; the
    /// lists say exactly what to add.
    /// </summary>
    private (ImmutableArray<string> Texts, ImmutableArray<string> Images) Needs(
        IEnumerable<AchievementDefinition> shown,
        HotelTexts known
    )
    {
        var lines = new SortedSet<string>(StringComparer.Ordinal);
        var images = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var definition in shown)
        {
            if (!known.TryGetText(CategoryKey(definition), out _))
                lines.Add($"quests.{definition.Category}.name={Title(definition.Category)}");
            foreach (var level in definition.Levels)
            {
                var badgeBase = AchievementBadgeCodes.BaseOf(level.BadgeCode);
                if (!HasText(known, BADGE_NAME, level.BadgeCode, badgeBase))
                    lines.Add($"badge_name_{badgeBase}={Title(definition.Key)}");
                if (!HasText(known, BADGE_DESC, level.BadgeCode, badgeBase))
                    lines.Add(
                        $"badge_desc_{badgeBase}=TODO: say how to earn it (%limit% is the level's goal)"
                    );
                if (!_badgeAssets.Exists(level.BadgeCode))
                    images.Add(_badgeAssets.Describe(level.BadgeCode));
            }
        }

        return ([.. lines], [.. images]);
    }

    private const string BADGE_NAME = "badge_name_";
    private const string BADGE_DESC = "badge_desc_";

    private static string CategoryKey(AchievementDefinition definition) =>
        $"quests.{definition.Category}.name";

    /// <summary>Every text <see cref="Needs"/> looks for, to read at once.</summary>
    private static IEnumerable<string> TextKeys(IEnumerable<AchievementDefinition> shown) =>
        shown.SelectMany(definition =>
            definition
                .Levels.SelectMany(x =>
                    new[] { x.BadgeCode, AchievementBadgeCodes.BaseOf(x.BadgeCode) }
                )
                .SelectMany(x => new[] { BADGE_NAME + x, BADGE_DESC + x })
                .Append(CategoryKey(definition))
        );

    private static bool HasText(HotelTexts known, string prefix, string code, string badgeBase) =>
        known.TryGetText(prefix + code, out _) || known.TryGetText(prefix + badgeBase, out _);

    private static string Title(string key) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            key.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ')
        );

    /// <summary>
    /// Whether two definitions are the same apart from their revision. Compared as JSON, because
    /// the level lists are immutable arrays whose record equality compares references.
    /// </summary>
    private static bool Same(AchievementDefinition existing, AchievementDefinition file) =>
        JsonSerializer.Serialize(existing with { Revision = 0 })
        == JsonSerializer.Serialize(file with { Revision = 0 });
}
