using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Admin.Configuration;
using Turbo.Admin.Players;
using Turbo.Admin.Rooms;
using Turbo.Database.Context;
using Turbo.Primitives.Gamedata;

namespace Turbo.Admin.Search;

/// <summary>
/// Which kinds of hit the panel's search may show a staff member: players and rooms with their
/// view nodes, catalog pages with the catalog's, texts and product data with gamedata's, and
/// furniture with either.
/// </summary>
public sealed record SearchScope(bool Players, bool Rooms, bool Catalog, bool Gamedata);

/// <summary>
/// The panel's search (Ctrl K): one term across players and rooms by name, catalog pages by
/// caption or name, furniture by class name, and texts and product data by key, code or wording,
/// the first few of each the staff member may see. Each kind asks what its own page asks, so a
/// hit there is a hit on that page too.
/// </summary>
public sealed class AdminSearchQueries(
    IDbContextFactory<TurboDbContext> database,
    AdminPlayerQueries players,
    AdminRoomQueries rooms,
    AdminCatalogQueries catalog,
    IGamedataTextService texts,
    IGamedataProductService products,
    IOptions<AdminConfig> config
)
{
    /// <summary>Shorter terms find too much to be any use; the panel opens ids itself.</summary>
    public const int MIN_TERM_LENGTH = 2;

    private const string ESCAPE = "\\";

    public async Task<SearchResponse> SearchAsync(
        string? text,
        SearchScope scope,
        CancellationToken ct
    )
    {
        var term = (text ?? string.Empty).Trim();

        if (term.Length < MIN_TERM_LENGTH)
            return new SearchResponse([]);

        if (term.Length > config.Value.RoomSearchMaxLength)
            term = term[..config.Value.RoomSearchMaxLength];

        var take = Math.Max(1, config.Value.SearchHitsPerKind);

        // Each kind reads through its own context, so they are asked at once.
        var found = await Task.WhenAll(
                scope.Players ? PlayersAsync(term, take, ct) : NoneAsync(),
                scope.Rooms ? RoomsAsync(term, take, ct) : NoneAsync(),
                scope.Catalog ? CatalogPagesAsync(term, take, ct) : NoneAsync(),
                scope.Catalog || scope.Gamedata
                    ? Task.FromResult(Furniture(term, take))
                    : NoneAsync(),
                scope.Gamedata ? TextsAsync(term, take, ct) : NoneAsync(),
                scope.Gamedata ? ProductsAsync(term, take, ct) : NoneAsync()
            )
            .ConfigureAwait(false);

        return new SearchResponse([.. found.OfType<SearchGroup>().Where(x => x.Hits.Length > 0)]);

        static Task<SearchGroup?> NoneAsync() => Task.FromResult<SearchGroup?>(null);
    }

    private async Task<SearchGroup?> PlayersAsync(string term, int take, CancellationToken ct)
    {
        var page = await players
            .SearchAsync(term, PlayerSearchMode.Name, onlineOnly: false, page: 1, ct)
            .ConfigureAwait(false);

        return new SearchGroup(
            "player",
            page.Total,
            [
                .. page
                    .Players.Take(take)
                    .Select(x => new SearchHit(
                        x.Id.ToString(),
                        x.Name,
                        Line($"#{x.Id}", x.IsOnline ? "online" : null, x.Motto)
                    )),
            ]
        );
    }

    private async Task<SearchGroup?> RoomsAsync(string term, int take, CancellationToken ct)
    {
        var page = await rooms
            .SearchAsync(term, RoomSearchMode.Name, page: 1, ct)
            .ConfigureAwait(false);

        return new SearchGroup(
            "room",
            page.Total,
            [
                .. page
                    .Rooms.Take(take)
                    .Select(x => new SearchHit(
                        x.Id.ToString(),
                        x.Name,
                        Line(
                            $"#{x.Id}",
                            $"owner {x.OwnerName}",
                            x.IsLoaded ? $"{x.Population} inside" : null
                        )
                    )),
            ]
        );
    }

    /// <summary>Catalog pages whose caption or open-by name has the term, in tree order.</summary>
    private async Task<SearchGroup?> CatalogPagesAsync(string term, int take, CancellationToken ct)
    {
        var like = $"%{Escape(term)}%";
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);

        await using var dbScope = db.ConfigureAwait(false);

        var matching = db
            .CatalogPages.AsNoTracking()
            .Where(x =>
                EF.Functions.Like(x.Localization, like, ESCAPE)
                || (x.Name != null && EF.Functions.Like(x.Name, like, ESCAPE))
            );
        var total = await matching.CountAsync(ct).ConfigureAwait(false);
        var pages = await matching
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Localization)
            .ThenBy(x => x.Id)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.Localization,
                x.Name,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new SearchGroup(
            "catalogPage",
            total,
            [
                .. pages.Select(x => new SearchHit(
                    x.Id.ToString(),
                    x.Localization,
                    Line($"#{x.Id}", x.Name)
                )),
            ]
        );
    }

    /// <summary>Furniture by the start of its class name, as the catalog editor's picker finds it.</summary>
    private SearchGroup? Furniture(string term, int take)
    {
        var found = catalog.SearchFurniture(term);

        return new SearchGroup(
            "furniture",
            found.Length,
            [
                .. found
                    .Take(take)
                    .Select(x => new SearchHit(x.Id.ToString(), x.Name, Line($"#{x.Id}", x.Type))),
            ]
        );
    }

    private async Task<SearchGroup?> TextsAsync(string term, int take, CancellationToken ct)
    {
        var page = await texts.SearchAsync(term, 0, ct).ConfigureAwait(false);

        return new SearchGroup(
            "text",
            page.Total,
            [.. page.Items.Take(take).Select(x => new SearchHit(x.Key, x.Key, x.Value))]
        );
    }

    private async Task<SearchGroup?> ProductsAsync(string term, int take, CancellationToken ct)
    {
        var page = await products.SearchAsync(term, 0, ct).ConfigureAwait(false);

        return new SearchGroup(
            "product",
            page.Total,
            [
                .. page
                    .Items.Take(take)
                    .Select(x => new SearchHit(x.Code, x.Code, x.Name ?? x.HabboName)),
            ]
        );
    }

    /// <summary>The parts of a hit's line that it has, joined.</summary>
    private static string? Line(params string?[] parts)
    {
        var line = string.Join(" · ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));

        return line.Length == 0 ? null : line;
    }

    private static string Escape(string term) =>
        term.Replace(ESCAPE, ESCAPE + ESCAPE, StringComparison.Ordinal)
            .Replace("%", ESCAPE + "%", StringComparison.Ordinal)
            .Replace("_", ESCAPE + "_", StringComparison.Ordinal);
}
