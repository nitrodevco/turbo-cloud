using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// Gives a definition the behaviour Sulake's furni data says its class has
/// (<see cref="FurnitureBehaviours"/>): the import applies it to each definition it makes, so a
/// fridge arrives handing drinks out and a crackable arrives knowing what it holds. The
/// <c>MapFurnitureBehaviours</c> migration did the same, once, for the definitions a hotel had
/// before it; a hotel that imports after it ran had nothing for it to map.
/// </summary>
internal static class FurnitureBehaviourApplier
{
    /// <summary>The known floor behaviour, preserving the resolved custom logic and wall behavior.</summary>
    public static string LogicOf(ProductType type, string className, string resolvedLogic)
    {
        if (type != ProductType.Floor || !FurnitureRecords.IsDefaultLogic(resolvedLogic))
            return resolvedLogic;

        var name = FurnitureBehaviours.BaseName(className);

        if (FurnitureBehaviours.CrackablesByName.ContainsKey(name))
            return FurnitureBehaviours.CRACKABLE;

        if (FurnitureBehaviours.VendingByName.ContainsKey(name))
            return FurnitureBehaviours.VENDING_MACHINE;

        return FurnitureBehaviours.LogicsByName.GetValueOrDefault(name) ?? resolvedLogic;
    }

    /// <summary>
    /// Gives the definition its class's behaviour, as the migration's SQL does: only a definition
    /// still on a default logic, or already on the behaviour's, is changed. Whether anything was.
    /// </summary>
    public static bool Apply(FurnitureDefinitionEntity definition)
    {
        if (definition.ProductType != ProductType.Floor)
            return false;

        var name = FurnitureBehaviours.BaseName(definition.Name);

        if (FurnitureBehaviours.CrackablesByName.TryGetValue(name, out var crackable))
        {
            if (!FurnitureBehaviours.MayTake(definition.Logic, FurnitureBehaviours.CRACKABLE))
                return false;

            definition.Logic = FurnitureBehaviours.CRACKABLE;
            definition.TotalStates = crackable.States;
            definition.UsagePolicy = (FurnitureUsageType)crackable.Usage;
            definition.CanWalk = definition.CanWalk || crackable.Walkable;
            definition.ExtraData = WithSection(
                definition.ExtraData,
                "crackable",
                crackable.Crackable
            );

            return true;
        }

        if (FurnitureBehaviours.VendingByName.TryGetValue(name, out var vending))
        {
            if (!FurnitureBehaviours.MayTake(definition.Logic, FurnitureBehaviours.VENDING_MACHINE))
                return false;

            definition.Logic = FurnitureBehaviours.VENDING_MACHINE;
            definition.UsagePolicy = (FurnitureUsageType)vending.Usage;
            definition.ExtraData = WithSection(definition.ExtraData, "vending", vending.Vending);

            return true;
        }

        if (FurnitureBehaviours.LogicsByName.TryGetValue(name, out var logic))
        {
            if (!FurnitureBehaviours.MayTake(definition.Logic, logic))
                return false;

            definition.Logic = logic;

            if (logic == FurnitureBehaviours.EFFECT_PROVIDER)
                definition.UsagePolicy = FurnitureUsageType.Everybody;

            return true;
        }

        return false;
    }

    /// <summary>The extra data with the section set, kept beside whatever else it holds.</summary>
    private static string WithSection(string? extraData, string section, string json)
    {
        JsonObject data;

        try
        {
            data = JsonNode.Parse(extraData ?? string.Empty) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            data = new JsonObject();
        }

        data[section] = JsonNode.Parse(json);

        return data.ToJsonString();
    }
}
