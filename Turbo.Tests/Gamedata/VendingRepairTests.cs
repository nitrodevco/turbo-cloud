using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Providers;
using Xunit;

namespace Turbo.Tests.Gamedata;

public sealed partial class GamedataImportTests
{
    [Fact]
    public async Task vending_preview_includes_only_known_missing_or_blocked_floor_defaults()
    {
        AddVending(100, "ads_711");
        AddVending(101, "ads_calip_cola", extraData: "{\"vending\":{\"handItems\":[]}}");
        AddVending(102, "ads_cheetos", extraData: "{\"vending\":{\"handItems\":[9999]}}");
        AddVending(103, "ads_chups", extraData: "{\"vending\":{\"handItems\":\"bad\"}}");
        AddVending(104, "ads_dfrisss", extraData: "{\"vending\":[]}");
        AddVending(
            105,
            "ads_pepsi0",
            extraData: "{\"vending\":{\"handItems\":[33],\"HandItems\":[]}}"
        );
        AddVending(106, "ads_clearasil_vend", extraData: "{}");
        AddVending(110, "ads_honeymonster", extraData: "not-json");
        AddVending(111, "ads_hh_safe", extraData: "{\"vending\":{\"handItems\":[59]}}");
        AddVending(112, "ads_oc_soda", extraData: "{\"vending\":{\"HandItems\":[9999]}}");
        AddVending(107, "ads_711", logic: "my_custom_logic");
        AddVending(108, "unknown_vending");
        var wall = MakeVending(109, "ads_711");
        wall.ProductType = ProductType.Wall;
        _db.Insert(wall);

        var preview = await _furniture.PreviewVendingRepairAsync(0, Ct);

        preview
            .Items.Select(x => x.Id)
            .Should()
            .BeEquivalentTo([100, 101, 103, 104, 105, 106, 110]);
        preview.Items.Single(x => x.Id == 100).After.Should().NotBeNull();
        preview.Items.Single(x => x.Id == 101).After.Should().NotBeNull();
        preview.Items.Single(x => x.Id == 103).After.Should().BeNull();
        preview.Items.Single(x => x.Id == 104).After.Should().BeNull();
        preview.Items.Single(x => x.Id == 105).After.Should().BeNull();
        preview.Items.Single(x => x.Id == 110).After.Should().BeNull();
        preview.Items.Single(x => x.Id == 106).After.Should().NotBeNull();
        preview.Items.Single(x => x.Id == 106).Animates.Should().BeFalse();
    }

    [Fact]
    public async Task vending_preview_clamps_pages_and_returns_the_last_page()
    {
        foreach (var variant in Enumerable.Range(0, 61))
            AddVending(200 + variant, $"ads_711*{variant + 1}");

        var first = await _furniture.PreviewVendingRepairAsync(0, Ct);
        var requestedLast = await _furniture.PreviewVendingRepairAsync(900, Ct);

        first.Total.Should().Be(61);
        first.Page.Should().Be(0);
        first.PageSize.Should().Be(60);
        first.Items.Should().HaveCount(60);
        requestedLast.Total.Should().Be(61);
        requestedLast.Page.Should().Be(1);
        requestedLast.PageSize.Should().Be(60);
        requestedLast.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task vending_preview_blocks_a_default_logic_when_its_class_has_registered_logic()
    {
        _fakes.Handlers["get_" + nameof(IRoomObjectLogicProvider.LogicTypes)] = _ =>
            LOGICS.Append("fridge").ToArray();
        AddVending(150, "fridge");

        var preview = await _furniture.PreviewVendingRepairAsync(0, Ct);

        var item = preview.Items.Single(x => x.Id == 150);
        item.After.Should().BeNull();
        item.Reason.Should().Contain("registered custom logic");
    }

    [Fact]
    public async Task vending_repair_preserves_existing_animation_and_large_extension_number()
    {
        const string extraData =
            "{\"extension\":{\"large\":9007199254740993},\"vending\":{\"animates\":false}}";
        AddVending(151, "ads_711", extraData: extraData);
        var preview = await _furniture.PreviewVendingRepairAsync(0, Ct);
        var item = preview.Items.Single(x => x.Id == 151);

        item.Animates.Should().BeFalse();
        item.After.Should().Contain("9007199254740993");
        var change = await _furniture.RepairVendingAsync([Select(preview, 151)], STAFF, Ct);

        change.Should().NotBeNull();
        var definition = (await DefinitionsAsync()).Single(x => x.Id == 151);
        var saved = JsonNode.Parse(definition.ExtraData!)!.AsObject();
        saved["extension"]!["large"]!.ToJsonString().Should().Be("9007199254740993");
        saved["vending"]!["animates"]!.GetValue<bool>().Should().BeFalse();
        saved["vending"]!["handItems"]!.AsArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task vending_repair_preserves_extensions_and_usage_and_can_be_rolled_back()
    {
        AddVending(
            120,
            "ads_711",
            extraData: "{\"extension\":{\"keep\":7},\"vending\":{\"note\":\"hotel\",\"handItems\":[]}}",
            usage: FurnitureUsageType.Everybody
        );
        AddVending(121, "ads_calip_cola", extraData: "{}", usage: FurnitureUsageType.Controller);
        var preview = await _furniture.PreviewVendingRepairAsync(0, Ct);

        var changes = await _furniture.RepairVendingAsync(
            [Select(preview, 120), Select(preview, 121, useDefault: true)],
            STAFF,
            Ct
        );

        changes.Should().NotBeNull();
        changes!.Kind.Should().Be(GamedataChangeKind.Edit);
        var definitions = await DefinitionsAsync();
        var repaired = definitions.Single(x => x.Id == 120);
        repaired.Logic.Should().Be("vending_machine");
        repaired.UsagePolicy.Should().Be(FurnitureUsageType.Everybody);
        var saved = JsonNode.Parse(repaired.ExtraData!)!.AsObject();
        saved["extension"]!["keep"]!.GetValue<int>().Should().Be(7);
        saved["vending"]!["note"]!.GetValue<string>().Should().Be("hotel");
        saved["vending"]!["handItems"]!.AsArray().Should().NotBeEmpty();
        saved["vending"]!["animates"]!.GetValue<bool>().Should().BeTrue();
        definitions
            .Single(x => x.Id == 121)
            .UsagePolicy.Should()
            .NotBe(FurnitureUsageType.Controller);

        await _history.RollbackAsync(changes.Id, STAFF, Ct);
        definitions = await DefinitionsAsync();
        definitions
            .Single(x => x.Id == 120)
            .ExtraData.Should()
            .Be("{\"extension\":{\"keep\":7},\"vending\":{\"note\":\"hotel\",\"handItems\":[]}}");
        definitions.Single(x => x.Id == 120).UsagePolicy.Should().Be(FurnitureUsageType.Everybody);
        definitions.Single(x => x.Id == 121).Logic.Should().Be("default_floor");
    }

    [Fact]
    public async Task stale_or_deleted_vending_selection_conflicts_without_partial_history()
    {
        AddVending(130, "ads_711");
        AddVending(131, "ads_calip_cola");
        var preview = await _furniture.PreviewVendingRepairAsync(0, Ct);
        var selection = Select(preview, 130);
        await _furniture.UpdateDefinitionAsync(
            130,
            new JsonObject { ["extradata"] = "{\"hotel\":true}" },
            STAFF,
            Ct
        );
        var countBefore = await ChangeSetCountAsync();

        var stale = () => _furniture.RepairVendingAsync([selection], STAFF, Ct);
        await stale.Should().ThrowAsync<VendingRepairConflictException>();

        var fresh = await _furniture.PreviewVendingRepairAsync(0, Ct);
        var deleted = Select(fresh, 131);
        await using (var ctx = await _db.CreateDbContextAsync(Ct))
        {
            var entity = await ctx.FurnitureDefinitions.SingleAsync(x => x.Id == 131, Ct);
            ctx.FurnitureDefinitions.Remove(entity);
            await ctx.SaveChangesAsync(Ct);
        }
        var mixed = () => _furniture.RepairVendingAsync([Select(fresh, 130), deleted], STAFF, Ct);
        await mixed.Should().ThrowAsync<VendingRepairConflictException>();
        (await ChangeSetCountAsync()).Should().Be(countBefore);
        (await DefinitionsAsync()).Single(x => x.Id == 130).Logic.Should().Be("default_floor");
    }

    [Fact]
    public async Task vending_repair_detects_edit_before_transaction_load_and_cannot_repeat()
    {
        AddVending(140, "ads_711");
        var preview = await _furniture.PreviewVendingRepairAsync(0, Ct);
        var selection = Select(preview, 140);
        _db.BeforeNextTransaction = async () =>
        {
            await using var ctx = await _db.CreateDbContextAsync(Ct);
            var row = await ctx.FurnitureDefinitions.SingleAsync(x => x.Id == 140, Ct);
            row.PublicName = "Concurrent edit";
            await ctx.SaveChangesAsync(Ct);
        };

        var race = () => _furniture.RepairVendingAsync([selection], STAFF, Ct);
        await race.Should().ThrowAsync<VendingRepairConflictException>();
        (await DefinitionsAsync()).Single().Logic.Should().Be("default_floor");

        var current = await _furniture.PreviewVendingRepairAsync(0, Ct);
        var accepted = Select(current, 140);
        var result = await _furniture.RepairVendingAsync([accepted], STAFF, Ct);
        var count = await ChangeSetCountAsync();
        var again = () => _furniture.RepairVendingAsync([accepted], STAFF, Ct);
        await again.Should().ThrowAsync<VendingRepairConflictException>();
        (await ChangeSetCountAsync()).Should().Be(count);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task vending_repair_validates_batch_arguments()
    {
        var empty = () => _furniture.RepairVendingAsync([], STAFF, Ct);
        await empty.Should().ThrowAsync<ArgumentException>();
        var invalid = () =>
            _furniture.RepairVendingAsync(
                [new VendingRepairSelectionSnapshot { Id = 0, Version = "x" }],
                STAFF,
                Ct
            );
        await invalid.Should().ThrowAsync<ArgumentException>();
        var duplicate = () =>
            _furniture.RepairVendingAsync(
                [
                    new VendingRepairSelectionSnapshot { Id = 1, Version = "x" },
                    new VendingRepairSelectionSnapshot { Id = 1, Version = "y" },
                ],
                STAFF,
                Ct
            );
        await duplicate.Should().ThrowAsync<ArgumentException>();

        var tooMany = Enumerable
            .Range(1, 101)
            .Select(id => new VendingRepairSelectionSnapshot { Id = id, Version = "preview" })
            .ToArray();
        var overLimit = () => _furniture.RepairVendingAsync(tooMany, STAFF, Ct);
        await overLimit.Should().ThrowAsync<ArgumentException>();
        (await DefinitionsAsync()).Should().BeEmpty();
        (await ChangeSetCountAsync()).Should().Be(0);
    }

    private static VendingRepairSelectionSnapshot Select(
        VendingRepairPreviewSnapshot preview,
        int id,
        bool useDefault = false
    ) =>
        new()
        {
            Id = id,
            Version = preview.Items.Single(x => x.Id == id).Version,
            UseDefaultUsagePolicy = useDefault,
        };

    private static FurnitureDefinitionEntity MakeVending(
        int id,
        string name,
        string logic = "default_floor",
        string? extraData = null,
        FurnitureUsageType usage = FurnitureUsageType.Controller
    ) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = logic,
            Width = 1,
            Length = 1,
            StackHeight = 0,
            CanStack = false,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = false,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            UsagePolicy = usage,
            ExtraData = extraData,
        };

    private void AddVending(
        int id,
        string name,
        string logic = "default_floor",
        string? extraData = null,
        FurnitureUsageType usage = FurnitureUsageType.Controller
    ) => _db.Insert(MakeVending(id, name, logic, extraData, usage));

    private async Task<int> ChangeSetCountAsync()
    {
        await using var ctx = await _db.CreateDbContextAsync(Ct);
        return await ctx.GamedataChangeSets.CountAsync(Ct);
    }
}
