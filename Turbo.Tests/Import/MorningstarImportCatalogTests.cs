using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Import.MorningstarImportPlayersTests;

namespace Turbo.Tests.Import;

/// <summary>What <c>--catalog</c> and <c>--chatlogs</c> add to an import, and that without them nothing of it is copied.</summary>
public class MorningstarImportCatalogTests
{
    private static MorningstarDb Hotel()
    {
        var ms = MorningstarImportPlayersTests.Hotel();

        ms.Insert(
                "room_models",
                new
                {
                    name = "model_a",
                    door_x = 1,
                    door_y = 1,
                    door_dir = 2,
                    heightmap = "xxxx\r\nx000",
                }
            )
            .Insert(
                "rooms",
                new
                {
                    id = 100,
                    owner_id = 10,
                    name = "Alice's place",
                    model = "model_a",
                    state = "open",
                    override_model = "0",
                }
            )
            .Insert(
                "items_base",
                new
                {
                    id = 1,
                    sprite_id = 30,
                    item_name = "chair_plasto",
                    type = "s",
                    interaction_type = "default",
                    width = 1,
                    length = 1,
                    allow_gift = "1",
                }
            )
            .Insert(
                "items_base",
                new
                {
                    id = 2,
                    sprite_id = 31,
                    item_name = "rare_dragonlamp",
                    type = "s",
                    interaction_type = "default",
                    width = 1,
                    length = 1,
                    allow_gift = "0",
                }
            )
            .Insert(
                "items_base",
                new
                {
                    id = 3,
                    sprite_id = 0,
                    item_name = "ADM",
                    type = "b",
                    interaction_type = "default",
                }
            )
            .Insert(
                "items_base",
                new
                {
                    id = 4,
                    sprite_id = 0,
                    item_name = "a0 pet0",
                    type = "p",
                    interaction_type = "default",
                }
            )
            .Insert(
                "catalog_pages",
                new
                {
                    id = 1,
                    parent_id = -1,
                    caption_save = "furni",
                    caption = "Furniture",
                    icon_image = 7,
                    visible = "1",
                    enabled = "1",
                    min_rank = 1,
                    order_num = 2,
                    page_layout = "default_3x3",
                    page_headline = "head.gif",
                    page_teaser = "teaser.gif",
                    page_special = "",
                    page_text1 = "Welcome",
                    page_text2 = "unused",
                    page_text_details = "Pick one",
                    page_text_teaser = "Nice",
                }
            )
            .Insert(
                "catalog_pages",
                new
                {
                    id = 2,
                    parent_id = 1,
                    caption_save = "front",
                    caption = "Front",
                    icon_image = 1,
                    visible = "1",
                    enabled = "1",
                    min_rank = 7,
                    order_num = 1,
                    page_layout = "frontpage",
                    page_headline = "h",
                    page_teaser = "t",
                    page_text1 = "one",
                    page_text2 = "two",
                    page_text_teaser = "tease",
                }
            )
            .Insert(
                "catalog_pages",
                new
                {
                    id = 3,
                    parent_id = 99,
                    caption = "Orphan",
                    visible = "1",
                    enabled = "1",
                    min_rank = 1,
                    page_layout = "default_3x3",
                }
            )
            .Insert(
                "catalog_items",
                new
                {
                    id = 10,
                    item_ids = "1",
                    page_id = 1,
                    catalog_name = "chair_plasto",
                    cost_credits = 3,
                    cost_points = 0,
                    points_type = 0,
                    amount = 1,
                    limited_stack = 0,
                    limited_sells = 0,
                    order_number = 1,
                    extradata = "",
                    have_offer = "1",
                    club_only = "0",
                }
            )
            .Insert(
                "catalog_items",
                new
                {
                    id = 11,
                    item_ids = "2",
                    page_id = 1,
                    catalog_name = "rare_dragonlamp",
                    cost_credits = 0,
                    cost_points = 50,
                    points_type = 5,
                    amount = 1,
                    limited_stack = 100,
                    limited_sells = 40,
                    order_number = 2,
                    extradata = "",
                    have_offer = "0",
                    club_only = "1",
                }
            )
            .Insert(
                "catalog_items",
                new
                {
                    id = 12,
                    item_ids = "1:2;3:1",
                    page_id = 2,
                    catalog_name = "starter_bundle",
                    cost_credits = 10,
                    cost_points = 0,
                    points_type = 0,
                    amount = 1,
                    limited_stack = 0,
                    limited_sells = 0,
                    order_number = 1,
                    extradata = "",
                    have_offer = "0",
                    club_only = "0",
                }
            )
            .Insert(
                "catalog_items",
                new
                {
                    id = 13,
                    item_ids = "4",
                    page_id = 1,
                    catalog_name = "a0 pet0",
                    cost_credits = 20,
                    cost_points = 0,
                    points_type = 0,
                    amount = 1,
                    limited_stack = 0,
                    limited_sells = 0,
                    order_number = 3,
                    extradata = "",
                    have_offer = "0",
                    club_only = "0",
                }
            )
            .Insert(
                "chatlogs_room",
                new
                {
                    room_id = 100,
                    user_from_id = 10,
                    user_to_id = 0,
                    message = "hi room",
                    timestamp = Unix(NOW.AddDays(-1)),
                }
            )
            .Insert(
                "chatlogs_room",
                new
                {
                    room_id = 555,
                    user_from_id = 10,
                    user_to_id = 0,
                    message = "gone room",
                    timestamp = Unix(NOW.AddDays(-1)),
                }
            )
            .Insert(
                "chatlogs_private",
                new
                {
                    user_from_id = 10,
                    user_to_id = 11,
                    message = "hey Bob",
                    timestamp = Unix(NOW.AddHours(-2)),
                }
            );

        return ms;
    }

    private static SqliteDb Target()
    {
        var target = new SqliteDb();

        SeedGroups(target);
        target.Insert(
            new CatalogPageEntity
            {
                Id = 1,
                Localization = "root",
                Icon = 0,
                Layout = "default_3x3",
                SortOrder = 0,
                Display = CatalogPageDisplay.Regular,
            }
        );

        return target;
    }

    [Fact]
    public async Task The_catalog_comes_across_under_turbos_root_with_its_offers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = Target();

        var options = Options() with { Catalog = true };
        var report = await ImportAsync(ms, target, options);

        await using var db = await target.CreateDbContextAsync(ct);

        var furni = await db.CatalogPages.SingleAsync(x => x.Name == "furni", ct);

        Assert.Equal(1, furni.ParentEntityId);
        Assert.Equal("Furniture", furni.Localization);
        Assert.Equal(7, furni.Icon);
        Assert.Equal(["head.gif", "teaser.gif", ""], furni.ImageData);
        Assert.Equal(["Welcome", "Pick one", "Nice"], furni.TextData);

        // frontpage is the client's frontpage4, with its own texts; rank 7 is its level 7.
        var front = await db.CatalogPages.SingleAsync(x => x.Name == "front", ct);

        Assert.Equal(furni.Id, front.ParentEntityId);
        Assert.Equal("frontpage4", front.Layout);
        Assert.Equal(["one", "two", "tease"], front.TextData);
        Assert.Equal(7, front.MinRank);
        Assert.False(await db.CatalogPages.AnyAsync(x => x.Localization == "Orphan", ct));

        var offers = await db.CatalogOffers.ToDictionaryAsync(x => x.LocalizationId, ct);

        Assert.Equal(3, offers["chair_plasto"].CostCredits);
        Assert.True(offers["chair_plasto"].CanBundle);

        var lamp = offers["rare_dragonlamp"];
        var diamonds = await db.CurrencyTypes.SingleAsync(x => x.ActivityPointType == 5, ct);

        Assert.Equal(
            (50, diamonds.Id, 1, false),
            (lamp.CostCurrency, lamp.CurrencyTypeId, lamp.ClubLevel, lamp.CanGift)
        );

        var series = await db.LtdSeries.SingleAsync(ct);

        Assert.Equal(
            (100, 60, true),
            (series.TotalQuantity, series.RemainingQuantity, series.IsActive)
        );

        // A bundle: two chairs and a badge.
        var bundle = await db
            .CatalogProducts.Where(x => x.CatalogOfferEntityId == offers["starter_bundle"].Id)
            .OrderBy(x => x.ProductType)
            .ToListAsync(ct);

        Assert.Equal(
            [(ProductType.Floor, 2, (string?)null), (ProductType.Badge, 1, "ADM")],
            bundle.Select(x => (x.ProductType, x.Quantity, x.ExtraParam))
        );

        // Pets are Turbo's own offers.
        Assert.False(offers.ContainsKey("a0 pet0"));
        Assert.Equal(
            1,
            report.NotesOf(
                "catalog offer left out: nothing in it Turbo sells this way (pets, bots, club)"
            )
        );
    }

    [Fact]
    public async Task Chat_logs_come_across_when_asked_for()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = Target();

        await ImportAsync(ms, target, Options() with { Chatlogs = true });

        await using var db = await target.CreateDbContextAsync(ct);

        Assert.Equal("hi room", (await db.Chatlogs.SingleAsync(ct)).Message);

        var message = await db.MessengerMessages.SingleAsync(ct);

        Assert.Equal(
            (10, 11, "hey Bob", true),
            (
                message.SenderPlayerEntityId,
                message.ReceiverPlayerEntityId,
                message.Message,
                message.Delivered
            )
        );
    }

    [Fact]
    public async Task Without_the_options_neither_is_copied()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = Target();

        await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        Assert.Equal(1, await db.CatalogPages.CountAsync(ct));
        Assert.False(await db.Chatlogs.AnyAsync(ct));
        Assert.False(await db.MessengerMessages.AnyAsync(ct));
    }
}
