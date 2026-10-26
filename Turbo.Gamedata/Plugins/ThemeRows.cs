using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Gamedata;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>The themes the hotel has (<see cref="ThemeChains"/>), with who gets each, as <c>gamedata_themes</c> keeps it.</summary>
internal static class ThemeRows
{
    public static Task<List<GamedataThemeEntity>> ReadAsync(
        TurboDbContext dbCtx,
        CancellationToken ct
    ) => dbCtx.GamedataThemes.AsNoTracking().ToListAsync(ct);

    /// <summary>
    /// Each theme loaded (<paramref name="loaded"/>, by key with the theme it extends) and the hotel's
    /// own, with its row; one with no row is Live, not the default and not to be chosen. A row of a
    /// theme that is not loaded gives nothing.
    /// </summary>
    public static IReadOnlyDictionary<string, ThemeAudience> Themes(
        IReadOnlyDictionary<string, string?> loaded,
        IEnumerable<GamedataThemeEntity> rows
    )
    {
        var byKey = rows.ToDictionary(x => x.PluginKey, StringComparer.OrdinalIgnoreCase);
        var keys = new Dictionary<string, string?>(loaded, StringComparer.OrdinalIgnoreCase);

        keys.TryAdd(ThemeChains.HOTEL, ThemeStyles.BASE);

        return keys.ToDictionary(
            x => x.Key,
            x => Of(x.Key, x.Value, byKey.GetValueOrDefault(x.Key)),
            StringComparer.OrdinalIgnoreCase
        );
    }

    public static ThemeAudience Of(string key, string? extends, GamedataThemeEntity? row) =>
        new(
            key,
            extends,
            row?.Status ?? ThemeStatus.Live,
            row?.IsDefault ?? false,
            row?.Selectable ?? false,
            row is null ? [] : Read<int>(row.AudiencePlayers),
            row is null ? [] : Read<string>(row.AudienceGroups)
        );

    public static string Write<T>(IEnumerable<T> values) => JsonSerializer.Serialize(values);

    private static ImmutableArray<T> Read<T>(string json)
    {
        try
        {
            var values = JsonSerializer.Deserialize<ImmutableArray<T>>(json);

            return values.IsDefault ? [] : values;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>What the rows are, cheaply: how many and when one last changed.</summary>
    public sealed record Stamp(int Count, DateTime? Latest);

    public static async Task<Stamp> StampAsync(TurboDbContext dbCtx, CancellationToken ct)
    {
        var rows = dbCtx.GamedataThemes.AsNoTracking();

        return new Stamp(
            await rows.CountAsync(ct).ConfigureAwait(false),
            await rows.MaxAsync(x => (DateTime?)x.UpdatedAt, ct).ConfigureAwait(false)
        );
    }
}
