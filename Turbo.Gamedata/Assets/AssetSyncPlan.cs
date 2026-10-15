using System;
using System.Collections.Generic;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// What a sync does with each of Habbo's libraries, against what the hotel knows of it: an upload
/// is left alone; one the hotel has at Habbo's revision (or failed at it in a way that won't pass)
/// is up to date, its ids brought to Habbo's; the rest are downloaded. The sync and its preview
/// both go by this, so the preview lists what the sync would take.
/// </summary>
internal static class AssetSyncPlan
{
    public static Plan Classify(
        IEnumerable<HabboLibrary> libraries,
        IReadOnlyDictionary<(AssetBundleKind, string), Known> known,
        Func<string, bool> isValidName
    )
    {
        var plan = new Plan();

        foreach (var library in libraries)
        {
            if (!isValidName(library.Name))
            {
                plan.Invalid++;

                continue;
            }

            if (known.TryGetValue((library.Kind, library.Name), out var row))
            {
                if (row.Source == AssetBundleSource.Upload)
                {
                    plan.Uploads++;

                    continue;
                }

                if (
                    row.Revision == library.Revision
                    && (row.HasFile || (row.Error is not null && !row.Retry))
                )
                {
                    plan.UpToDate++;

                    if (row.Ids != library.Ids)
                        plan.IdsChanged.Add(library);

                    continue;
                }
            }

            plan.Todo.Add(
                new Pending(
                    library,
                    row is { HasFile: true } ? AssetSyncReason.Updated : AssetSyncReason.New,
                    row
                )
            );
        }

        return plan;
    }

    /// <summary>What the hotel knows of a library before the sync.</summary>
    public sealed record Known(
        string? Revision,
        AssetBundleSource Source,
        bool HasFile,
        string? Error,
        bool Retry,
        string? Ids
    );

    /// <summary>A library to download, why, and the hotel's row of it when it has one.</summary>
    public sealed record Pending(HabboLibrary Library, AssetSyncReason Reason, Known? Row);

    public sealed class Plan
    {
        /// <summary>The libraries to download, in Habbo's order.</summary>
        public List<Pending> Todo { get; } = [];

        /// <summary>Up-to-date libraries whose ids Habbo now lists otherwise.</summary>
        public List<HabboLibrary> IdsChanged { get; } = [];

        public int UpToDate { get; set; }

        /// <summary>Libraries staff uploaded, which a sync never replaces.</summary>
        public int Uploads { get; set; }

        /// <summary>Names a bundle can't have; skipped.</summary>
        public int Invalid { get; set; }
    }
}
