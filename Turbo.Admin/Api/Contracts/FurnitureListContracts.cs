using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The words the panel reads and sends for a furniture definition's tags (<c>hotel-own</c>,
/// <c>no-bundle</c>, <c>not-sold</c>, ...), mapped one by one so renaming a member can't change
/// what the panel is sent.
/// </summary>
public static class FurnitureTagNames
{
    private static readonly (FurnitureTag Tag, string Name)[] NAMES =
    [
        (FurnitureTag.HotelOwn, "hotel-own"),
        (FurnitureTag.HabboUpdate, "habbo-update"),
        (FurnitureTag.Edited, "edited"),
        (FurnitureTag.NoBundle, "no-bundle"),
        (FurnitureTag.BundleFailed, "bundle-failed"),
        (FurnitureTag.BundleOutdated, "bundle-outdated"),
        (FurnitureTag.NotSold, "not-sold"),
    ];

    public static string Of(FurnitureTag tag) =>
        NAMES.FirstOrDefault(x => x.Tag == tag).Name
        ?? throw new ArgumentOutOfRangeException(nameof(tag), tag, null);

    /// <summary>The tag the panel named; false for a word that is not one.</summary>
    public static bool TryParse(string? value, out FurnitureTag tag)
    {
        var found = NAMES.FirstOrDefault(x => x.Name == value?.Trim().ToLowerInvariant());

        tag = found.Tag;

        return found.Name is not null;
    }

    /// <summary>The kind the panel named: <c>floor</c> or <c>wall</c>; false for a word that is not one.</summary>
    public static bool TryParseType(string? value, out ProductType type)
    {
        ProductType? parsed = value?.Trim().ToLowerInvariant() switch
        {
            "floor" => ProductType.Floor,
            "wall" => ProductType.Wall,
            _ => null,
        };

        type = parsed ?? default;

        return parsed is not null;
    }
}

/// <summary>A furniture definition in the list (<c>FurnitureListItem</c>).</summary>
public sealed record FurnitureListItemResponse(
    int Id,
    int ProductType,
    string ClassName,
    string? Name,
    int SpriteId,
    string? FurniLine,
    string? Category,
    string AssetName,
    string[] Tags
)
{
    public static FurnitureListItemResponse From(FurnitureListItem item) =>
        new(
            item.Id,
            (int)item.ProductType,
            item.ClassName,
            item.Name,
            item.SpriteId,
            item.FurniLine,
            item.Category,
            item.AssetName,
            [.. item.Tags.Select(FurnitureTagNames.Of)]
        );
}

/// <summary>A page of furniture definitions, with how many of those searched carry each tag.</summary>
public sealed record FurnitureListResponse(
    FurnitureListItemResponse[] Items,
    int Total,
    int PageSize,
    Dictionary<string, int> TagCounts,
    int Searched
)
{
    public static FurnitureListResponse From(FurnitureListPage page) =>
        new(
            [.. page.Items.Select(FurnitureListItemResponse.From)],
            page.Total,
            page.PageSize,
            page.TagCounts.ToDictionary(x => FurnitureTagNames.Of(x.Key), x => x.Value),
            page.Searched
        );
}

/// <summary>
/// A furniture definition to edit (<c>FurnitureDefinition</c>): as <see cref="FurnitureDefinitionDetail"/>,
/// its tags in the list's words.
/// </summary>
public sealed record FurnitureDefinitionResponse(
    int Id,
    int ProductType,
    string ClassName,
    string Item,
    string? Habbo,
    string? HabboFile,
    bool HabboFileRead,
    int States,
    string AssetName,
    string[] Tags
)
{
    public static FurnitureDefinitionResponse From(FurnitureDefinitionDetail detail) =>
        new(
            detail.Id,
            (int)detail.ProductType,
            detail.ClassName,
            detail.Item,
            detail.Habbo,
            detail.HabboFile,
            detail.HabboFileRead,
            detail.States,
            detail.AssetName,
            [.. detail.Tags.Select(FurnitureTagNames.Of)]
        );
}
