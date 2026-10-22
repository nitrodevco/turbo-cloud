using System.Linq;
using System.Text.Json.Nodes;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A definition as its bundle's own data says it would be (<see cref="FurnitureDraft"/>): the
/// fields to make it with and what the asset data said, each a JSON object; its colours past the
/// plain one; the definitions already drawn from the bundle.
/// </summary>
public sealed record FurnitureDraftResponse(
    string Bundle,
    int ProductType,
    string Fields,
    string File,
    int[] Colors,
    FurnitureDraftDefinitionResponse[] Existing
)
{
    public static FurnitureDraftResponse From(FurnitureDraft draft) =>
        new(
            draft.Bundle,
            (int)draft.ProductType,
            draft.Fields,
            draft.File,
            [.. draft.Colors],
            [
                .. draft.Existing.Select(x => new FurnitureDraftDefinitionResponse(
                    x.Id,
                    (int)x.ProductType,
                    x.ClassName
                )),
            ]
        );
}

public sealed record FurnitureDraftDefinitionResponse(int Id, int ProductType, string ClassName);

/// <summary>
/// Definitions to make: <c>type</c> <c>floor</c> or <c>wall</c>, the fields by furnidata and server
/// key (<c>classname</c> needed), and the colours to make a <c>classname*N</c> of.
/// </summary>
public sealed record FurnitureDefinitionCreateRequest(
    string? Type,
    JsonObject? Fields,
    int[]? Colors
);

/// <summary>The ids made, the plain definition first, and that definition as it now is.</summary>
public sealed record FurnitureDefinitionCreateResponse(
    int[] Ids,
    FurnitureDefinitionResponse Definition
);
