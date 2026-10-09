using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Hotel;
using Turbo.Database.Extensions;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Hotel.Snapshots;

namespace Turbo.Catalog.Reception;

/// <summary>
/// The promo articles (<see cref="IPromoArticleService"/>), kept in <c>promo_articles</c>. Every
/// article is read at once and kept for <see cref="ReceptionConfig.CacheSeconds"/>; what players
/// see is filtered from that by the time asked, so an article's dates take effect on time.
/// </summary>
public sealed class PromoArticleService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<CatalogConfig> config,
    TimeProvider time,
    ILogger<PromoArticleService> logger
) : IPromoArticleService
{
    private readonly ReceptionConfig _config = config.Value.Reception;

    private (DateTimeOffset ReadAt, ImmutableArray<PromoArticleSnapshot> Articles)? _read;

    public async Task<ImmutableArray<PromoArticleSnapshot>> GetLiveAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var read = _read;

        if (read is null || now - read.Value.ReadAt >= TimeSpan.FromSeconds(_config.CacheSeconds))
        {
            read = (now, await ListAsync(ct).ConfigureAwait(false));
            _read = read;
        }

        return
        [
            .. read
                .Value.Articles.Where(x => x.IsLive(now.UtcDateTime))
                .Take(Math.Max(0, _config.PromoArticleLimit)),
        ];
    }

    public async Task<ImmutableArray<PromoArticleSnapshot>> ListAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .PromoArticles.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. rows.Select(x => x.ToSnapshot())];
    }

    public async Task<PromoArticleSnapshot> SaveAsync(
        PromoArticleSnapshot article,
        CancellationToken ct
    )
    {
        Check(article);

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        PromoArticleEntity row;

        if (article.Id == 0)
        {
            var last = await dbCtx
                .PromoArticles.Select(x => (int?)x.SortOrder)
                .MaxAsync(ct)
                .ConfigureAwait(false);

            row = new PromoArticleEntity
            {
                Title = "",
                BodyText = "",
                ButtonText = "",
                LinkContent = "",
                ImageUrl = "",
                SortOrder = (last ?? -1) + 1,
            };
            dbCtx.PromoArticles.Add(row);
        }
        else
        {
            row =
                await dbCtx
                    .PromoArticles.FirstOrDefaultAsync(x => x.Id == article.Id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException(
                    $"There is no promo article {article.Id}.",
                    nameof(article)
                );
        }

        row.Title = article.Title.Trim();
        row.BodyText = article.BodyText.Trim();
        row.ButtonText = article.ButtonText.Trim();
        row.LinkType = article.LinkType;
        row.LinkContent = article.LinkContent.Trim();
        row.ImageUrl = article.ImageUrl.Trim();
        row.Visible = article.Visible;
        row.StartsAt = article.StartsAt;
        row.EndsAt = article.EndsAt;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        Invalidate();
        logger.LogInformation("Promo article {ArticleId} saved", row.Id);

        return row.ToSnapshot();
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .PromoArticles.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        dbCtx.PromoArticles.Remove(row);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        Invalidate();
        logger.LogInformation("Promo article {ArticleId} removed", id);

        return true;
    }

    public async Task ReorderAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .PromoArticles.OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var named = ids.Distinct()
            .Select(id => rows.FirstOrDefault(x => x.Id == id))
            .OfType<PromoArticleEntity>();
        var order = named.Concat(rows.Where(x => !ids.Contains(x.Id))).ToList();

        for (var i = 0; i < order.Count; i++)
            order[i].SortOrder = i;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        Invalidate();
    }

    /// <summary>What the next ask reads again.</summary>
    private void Invalidate() => _read = null;

    private static void Check(PromoArticleSnapshot article)
    {
        if (string.IsNullOrWhiteSpace(article.Title))
            throw new ArgumentException("An article needs a title.", nameof(article));

        Length(article.Title, PromoArticleEntity.TITLE_MAX_LENGTH, "title");
        Length(article.BodyText, PromoArticleEntity.BODY_MAX_LENGTH, "text");
        Length(article.ButtonText, PromoArticleEntity.BUTTON_MAX_LENGTH, "button");
        Length(article.LinkContent, PromoArticleEntity.LINK_MAX_LENGTH, "link");
        Length(article.ImageUrl, PromoArticleEntity.IMAGE_MAX_LENGTH, "picture");

        if (!Enum.IsDefined(article.LinkType))
            throw new ArgumentException("That is no kind of link.", nameof(article));

        if (article.StartsAt is { } starts && article.EndsAt is { } ends && ends <= starts)
            throw new ArgumentException("An article ends after it starts.", nameof(article));
    }

    private static void Length(string text, int max, string what)
    {
        if (text.Trim().Length > max)
            throw new ArgumentException($"The {what} can be at most {max} characters.", what);
    }
}
