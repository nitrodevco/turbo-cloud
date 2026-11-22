using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities;
using Turbo.Import.Morningstar.Steps;
using Turbo.Import.Source;
using Turbo.Import.Target;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Import.Morningstar;

/// <summary>
/// What every step of one import shares: both databases, the report, and what earlier steps
/// copied, by id, so a later step keeps only rows whose owner, room or furniture made it across.
/// Morningstar's ids are kept as Turbo's (a player's id, a room's, an item's), so nothing outside
/// the database that names one (a CMS's tickets, a link to a room) has to change.
/// </summary>
internal sealed class ImportContext(
    SourceDatabase source,
    TurboDbContext db,
    MorningstarImportOptions options,
    ImportReport report,
    ILogger log,
    DateTime now
)
{
    public SourceDatabase Source => source;

    public TurboDbContext Db => db;

    public MorningstarImportOptions Options => options;

    public ImportReport Report => report;

    public ILogger Log => log;

    /// <summary>When the import began (UTC): the time of rows Morningstar kept no time for.</summary>
    public DateTime Now => now;

    public TableWriter Writer { get; } = new(db, options.DryRun);

    /// <summary>The players copied.</summary>
    public HashSet<int> Players { get; } = [];

    /// <summary>Each player's <c>users_settings</c>, as far as the import reads it.</summary>
    public Dictionary<int, UserSettings> UserSettings { get; set; } = [];

    /// <summary>The rooms copied, and who owns each.</summary>
    public Dictionary<int, int> RoomOwners { get; } = [];

    /// <summary>
    /// Each room's <c>moodlight_data</c>: Morningstar keeps a dimmer's presets on its room,
    /// Turbo on the dimmer.
    /// </summary>
    public Dictionary<int, string> RoomMoodlights { get; } = [];

    /// <summary>The groups copied, by id, with the room each belongs to.</summary>
    public Dictionary<int, int> GuildRooms { get; } = [];

    /// <summary>Every Morningstar <c>items_base</c> row, by id.</summary>
    public Dictionary<int, BaseItem> BaseItems { get; } = [];

    /// <summary>
    /// Every <c>catalog_clothing</c> row, by id: the figure sets a clothing furni (by its name)
    /// or a player's purchase (by the id) unlocks.
    /// </summary>
    public Dictionary<int, ClothingItem> Clothing { get; } = [];

    /// <summary>Each player's name, for the furni that show one (a rentable space's renter).</summary>
    public Dictionary<int, string> PlayerNames { get; } = [];

    /// <summary>Turbo's furniture definition for each Morningstar <c>items_base</c> id.</summary>
    public Dictionary<int, DefinitionRef> Definitions { get; } = [];

    /// <summary>Turbo's room model for each Morningstar model name, in any case.</summary>
    public Dictionary<string, int> RoomModels { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Turbo's navigator category for each Morningstar one.</summary>
    public Dictionary<int, int> FlatCategories { get; } = [];

    /// <summary>Turbo's currency type for each Morningstar currency: -1 is credits, then the point types.</summary>
    public Dictionary<int, int> CurrencyTypes { get; } = [];

    /// <summary>Turbo's song for each Morningstar <c>soundtracks</c> id: what a music disc plays.</summary>
    public Dictionary<int, int> Songs { get; } = [];

    public Batch<T> Batch<T>(string table, int size = 5_000)
        where T : class => new(Writer, report, log, table, size);

    private int _provisionalId;

    /// <summary>
    /// Adds one row of the hotel's setup (a currency, a model, a definition) through EF and
    /// answers its id. A dry run writes nothing and answers a provisional id below zero, so the
    /// steps after it still have something to point at.
    /// </summary>
    public async Task<int> AddSetupAsync<T>(T entity, string table, CancellationToken ct)
        where T : TurboEntity
    {
        entity.CreatedAt = now;

        if (options.DryRun)
        {
            report.Wrote(table, 1);

            return --_provisionalId;
        }

        db.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        db.ChangeTracker.Clear();
        report.Wrote(table, 1);

        return entity.Id;
    }

    /// <summary>The Morningstar id of credits in <see cref="CurrencyTypes"/>.</summary>
    public const int CREDITS = -1;
}

/// <summary>A <c>catalog_clothing</c> row: a clothing item's name and the figure sets it unlocks.</summary>
internal sealed record ClothingItem(string Name, IReadOnlyList<int> SetIds);

/// <summary>A Turbo furniture definition as an import needs it.</summary>
internal sealed record DefinitionRef(
    int Id,
    string Name,
    string Logic,
    ProductType Type,
    FurnitureCategory Category,
    string SourceInteraction
);
