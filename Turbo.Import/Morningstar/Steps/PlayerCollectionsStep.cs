using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Messenger;
using Turbo.Database.Entities.Players;
using Turbo.Import.Morningstar.Formats;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// What a player has many of that needs no room: badges (<c>users_badges</c>), saved outfits
/// (<c>users_wardrobe</c>), avatar effects (<c>users_effects</c>), bought clothing
/// (<c>users_clothing</c>, through <c>catalog_clothing</c>'s figure set ids), ignored players
/// (<c>users_ignored</c>) and saved navigator searches (<c>users_saved_searches</c>).
/// </summary>
internal sealed class PlayerCollectionsStep : IImportStep
{
    private const int OUTFIT_FIGURE_MAX_LENGTH = PlayerOutfitEntity.FIGURE_MAX_LENGTH;

    public string Name => "badges, outfits, effects, clothing";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        await BadgesAsync(ctx, ct).ConfigureAwait(false);
        await OutfitsAsync(ctx, ct).ConfigureAwait(false);
        await EffectsAsync(ctx, ct).ConfigureAwait(false);
        await ClothingAsync(ctx, ct).ConfigureAwait(false);
        await IgnoredAsync(ctx, ct).ConfigureAwait(false);
        await SavedSearchesAsync(ctx, ct).ConfigureAwait(false);
    }

    private static async Task BadgesAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<PlayerBadgeEntity>("player_badges");
        var held = new HashSet<(int, string)>();

        await foreach (
            var row in ctx.Source.ReadAsync("users_badges", "id", ct).ConfigureAwait(false)
        )
        {
            var player = row.Int("user_id");
            var code = row.Text("badge_code").Trim();

            if (!ctx.Players.Contains(player) || code.Length == 0)
                continue;

            // The unique index compares codes as the database's collation does.
            if (!held.Add((player, code.ToUpperInvariant())))
                continue;

            var slot = row.Int("slot_id");

            await batch
                .AddAsync(
                    new PlayerBadgeEntity
                    {
                        CreatedAt = ctx.Now,
                        PlayerEntityId = player,
                        BadgeCode = code,
                        SlotId = slot >= 1 && slot <= ctx.Options.Limits.ActiveBadgesMax ? slot : 0,
                        PlayerEntity = null!,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task OutfitsAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<PlayerOutfitEntity>("player_outfits");
        var slots = new HashSet<(int, int)>();

        await foreach (
            var row in ctx.Source.ReadAsync("users_wardrobe", "slot_id", ct).ConfigureAwait(false)
        )
        {
            var player = row.Int("user_id");
            var slot = row.Int("slot_id");
            var figure = row.Text("look").Trim();

            if (!ctx.Players.Contains(player) || figure.Length == 0 || !slots.Add((player, slot)))
                continue;

            if (figure.Length > OUTFIT_FIGURE_MAX_LENGTH)
            {
                ctx.Report.Note(
                    $"saved outfit left out: longer than the {OUTFIT_FIGURE_MAX_LENGTH} characters Turbo keeps",
                    $"user {player} slot {slot}"
                );

                continue;
            }

            await batch
                .AddAsync(
                    new PlayerOutfitEntity
                    {
                        CreatedAt = ctx.Now,
                        PlayerEntityId = player,
                        SlotId = slot,
                        Figure = figure,
                        Gender = Genders.Of(row.Text("gender")),
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Morningstar keeps one row an effect: <c>total</c> copies of <c>duration</c> seconds each,
    /// the first running since <c>activation_timestamp</c> when that is 0 or more, and a duration
    /// of 0 or less for an effect owned for good.
    /// </summary>
    private static async Task EffectsAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<PlayerEffectEntity>("player_effects");
        var held = new HashSet<(int, int)>();
        var now = new DateTimeOffset(ctx.Now).ToUnixTimeSeconds();

        await foreach (
            var row in ctx.Source.ReadAsync("users_effects", null, ct).ConfigureAwait(false)
        )
        {
            var player = row.Int("user_id");
            var effect = row.Int("effect");

            if (!ctx.Players.Contains(player) || effect <= 0 || !held.Add((player, effect)))
                continue;

            var duration = row.Long("duration");
            var activated = row.Long("activation_timestamp", -1);
            var total = row.Int("total", 1);
            var entity = new PlayerEffectEntity
            {
                CreatedAt = ctx.Now,
                PlayerEntityId = player,
                EffectId = effect,
                PlayerEntity = null!,
            };

            if (duration <= 0)
                entity.IsPermanent = true;
            else
            {
                if (activated >= 0 && activated + duration > now)
                {
                    entity.ExpiresAt = DateTimeOffset
                        .FromUnixTimeSeconds(activated + duration)
                        .UtcDateTime;
                    total--;
                }
                else if (activated >= 0)
                    total--; // That copy ran out while the hotel was not counting.

                entity.InactiveCount = Math.Max(0, total);

                if (entity.ExpiresAt is null && entity.InactiveCount == 0)
                    continue;
            }

            await batch.AddAsync(entity, ct).ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task ClothingAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<PlayerFigureSetEntity>("player_figure_sets");
        var held = new HashSet<(int, int)>();

        await foreach (
            var row in ctx.Source.ReadAsync("users_clothing", null, ct).ConfigureAwait(false)
        )
        {
            var player = row.Int("user_id");

            if (!ctx.Players.Contains(player))
                continue;

            if (!ctx.Clothing.TryGetValue(row.Int("clothing_id"), out var clothing))
            {
                ctx.Report.Note(
                    "clothing left out: catalog_clothing has no such item",
                    $"user {player} clothing {row.Int("clothing_id")}"
                );

                continue;
            }

            foreach (var set in clothing.SetIds)
                if (held.Add((player, set)))
                    await batch
                        .AddAsync(
                            new PlayerFigureSetEntity
                            {
                                CreatedAt = ctx.Now,
                                PlayerEntityId = player,
                                SetId = set,
                            },
                            ct
                        )
                        .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task IgnoredAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<MessengerIgnoredEntity>("messenger_ignored");
        var seen = new HashSet<(int, int)>();

        await foreach (
            var row in ctx.Source.ReadAsync("users_ignored", null, ct).ConfigureAwait(false)
        )
        {
            var player = row.Int("user_id");
            var target = row.Int("target_id");

            if (
                player == target
                || !ctx.Players.Contains(player)
                || !ctx.Players.Contains(target)
                || !seen.Add((player, target))
            )
                continue;

            await batch
                .AddAsync(
                    new MessengerIgnoredEntity
                    {
                        CreatedAt = ctx.Now,
                        PlayerEntityId = player,
                        IgnoredPlayerEntityId = target,
                        PlayerEntity = null!,
                        IgnoredPlayerEntity = null!,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task SavedSearchesAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<PlayerNavigatorSavedSearchEntity>("player_navigator_saved_searches");

        await foreach (
            var row in ctx.Source.ReadAsync("users_saved_searches", "id", ct).ConfigureAwait(false)
        )
        {
            var player = row.Int("user_id");
            var code = row.Text("search_code").Trim();
            var filter = row.Text("filter").Trim();

            if (!ctx.Players.Contains(player) || code.Length == 0)
                continue;

            if (
                code.Length > PlayerNavigatorSavedSearchEntity.SEARCH_CODE_MAX_LENGTH
                || filter.Length > PlayerNavigatorSavedSearchEntity.FILTER_MAX_LENGTH
            )
            {
                ctx.Report.Note("saved search left out: too long", $"user {player} '{code}'");

                continue;
            }

            // The client names a saved search by an id unique to its player; the row's id is.
            await batch
                .AddAsync(
                    new PlayerNavigatorSavedSearchEntity
                    {
                        CreatedAt = ctx.Now,
                        PlayerEntityId = player,
                        SearchId = row.Int("id"),
                        SearchCode = code,
                        Filter = filter,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }
}
