using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// How one of Habbo's items meets the hotel's definition of it, field by field, three ways:
/// Habbo's new value, Habbo's value when it was last taken in (the base), and the definition's.
/// <list type="bullet">
/// <item>The definition still has Habbo's old value: it takes the new one.</item>
/// <item>The hotel changed the field and Habbo changed it too: the hotel's value is kept, and
/// the field is reported (<see cref="FurnitureFieldChange.Kept"/>).</item>
/// <item>Only the hotel changed it: nothing to say.</item>
/// </list>
/// Without a base - the first import of a definition the hotel already had - nothing tells the
/// hotel's changes from Habbo's, so a column only the client's furnidata has
/// (<see cref="FurnitureField.ClientOnly"/>) takes Habbo's value, and so does one its asset file
/// gives (<see cref="FurnitureField.FromFile"/>: <c>states</c>) - what the hotel had there came
/// from wherever its definitions came from, and the file is the authority. Every other column
/// keeps the hotel's value and is reported.
/// </summary>
internal static class FurnitureMerge
{
    public static ImmutableArray<FurnitureFieldChange> Compare(
        FurnitureDefinitionEntity definition,
        JsonObject habbo,
        JsonObject? @base
    )
    {
        var isWall = definition.ProductType == ProductType.Wall;
        var changes = ImmutableArray.CreateBuilder<FurnitureFieldChange>();

        foreach (var field in FurnitureFields.All)
        {
            if (!field.AppliesTo(isWall) || !field.IsIn(habbo))
                continue;

            var incoming = field.Normalize(habbo[field.Key]);
            var current = field.Read(definition);

            if (FurnitureField.Same(incoming, current))
                continue;

            bool kept;

            // A base taken in before its file was read says nothing of what comes from the file.
            if (@base is not null && field.IsIn(@base))
            {
                var previous = field.Normalize(@base[field.Key]);

                if (FurnitureField.Same(current, previous))
                    kept = false;
                else if (!FurnitureField.Same(incoming, previous))
                    kept = true;
                else
                    continue;
            }
            else
            {
                kept = !(field.ClientOnly || field.FromFile);
            }

            changes.Add(
                new FurnitureFieldChange
                {
                    Field = field.Key,
                    Current = FurnitureField.Text(current),
                    Incoming = FurnitureField.Text(incoming),
                    Kept = kept,
                }
            );
        }

        return changes.ToImmutable();
    }
}
