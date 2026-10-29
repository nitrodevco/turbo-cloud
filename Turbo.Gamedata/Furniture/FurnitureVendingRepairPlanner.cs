using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Gamedata.Furniture;

/// <summary>Proposes only missing vending fields; existing definitions never use the import applier.</summary>
internal static class FurnitureVendingRepairPlanner
{
    internal static readonly string[] FIELDS = ["logic", "extradata", "usagepolicy"];

    internal static VendingRepairItemSnapshot? Plan(
        FurnitureDefinitionEntity definition,
        IReadOnlySet<string> registered
    )
    {
        if (
            definition.ProductType != ProductType.Floor
            || !FurnitureBehaviours.MayTake(definition.Logic, FurnitureBehaviours.VENDING_MACHINE)
            || !FurnitureBehaviours.VendingByName.TryGetValue(
                FurnitureBehaviours.BaseName(definition.Name),
                out var known
            )
        )
            return null;

        var before = FurnitureRecords.Fields(definition, FIELDS).ToJsonString();
        var version = Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(
                        new
                        {
                            definition.Id,
                            definition.ProductType,
                            Definition = FurnitureRecords.Created(definition),
                            known.Usage,
                            known.Vending,
                        }
                    )
                )
            )
        );
        var defaults = JsonNode.Parse(known.Vending)!.AsObject();
        var ids = defaults["handItems"]!
            .AsArray()
            .Select(x => x!.GetValue<int>())
            .ToImmutableArray();
        var animates = defaults["animates"]?.GetValue<bool>() ?? true;
        var proposed = new VendingRepairItemSnapshot
        {
            Id = definition.Id,
            ClassName = definition.Name,
            Version = version,
            Before = before,
            HandItems = ids,
            Animates = animates,
            CurrentUsagePolicy = (int)definition.UsagePolicy,
            DefaultUsagePolicy = known.Usage,
        };

        var registeredLogic = FurnitureRecords.LogicFor(
            definition.ProductType,
            definition.Name,
            registered
        );
        if (
            definition.Logic != FurnitureBehaviours.VENDING_MACHINE
            && !FurnitureRecords.IsDefaultLogic(registeredLogic)
            && registeredLogic != FurnitureBehaviours.VENDING_MACHINE
        )
            return proposed with
            {
                Reason =
                    "This classname has a registered custom logic. Review its behaviour manually.",
            };

        try
        {
            var root = string.IsNullOrWhiteSpace(definition.ExtraData)
                ? new JsonObject()
                : JsonNode.Parse(definition.ExtraData) as JsonObject;
            if (root is null)
                return proposed with
                {
                    Reason = "Extra data must be a JSON object. Correct it in Behaviour first.",
                };
            var hasSection = root.TryGetPropertyValue("vending", out var saved);
            if (hasSection && saved is not JsonObject)
                return proposed with
                {
                    Reason =
                        "The vending section must be an object. Correct it in Behaviour first.",
                };
            var section = saved as JsonObject ?? new JsonObject();
            var properties = section.ToArray();
            foreach (var key in new[] { "handItems", "animates" })
                if (
                    properties.Count(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) > 1
                )
                    return proposed with
                    {
                        Reason =
                            "The vending section has duplicate fields. Correct them in Behaviour first.",
                    };

            var handItems = properties.FirstOrDefault(x =>
                x.Key.Equals("handItems", StringComparison.OrdinalIgnoreCase)
            );
            var animation = properties.FirstOrDefault(x =>
                x.Key.Equals("animates", StringComparison.OrdinalIgnoreCase)
            );
            if (animation.Key is not null)
            {
                if (
                    animation.Value is not JsonValue value
                    || !value.TryGetValue<bool>(out animates)
                )
                    return proposed with
                    {
                        Reason =
                            "The saved animation value is invalid. Correct it in Behaviour first.",
                    };
            }
            if (handItems.Key is not null)
            {
                if (
                    handItems.Value is not JsonArray list
                    || list.Any(x =>
                        x is not JsonValue value || !value.TryGetValue<int>(out var id) || id <= 0
                    )
                )
                    return proposed with
                    {
                        Reason = "The saved item list is invalid. Correct it in Behaviour first.",
                    };
                if (list.Count > 0)
                    return null; // Custom IDs and their weights are already configured.
                section.Remove(handItems.Key);
            }
            section["handItems"] = defaults["handItems"]!.DeepClone();
            if (animation.Key is null)
                section["animates"] = animates;
            if (!hasSection)
                root["vending"] = section;
            var after = JsonNode.Parse(before)!.AsObject();
            after["logic"] = FurnitureBehaviours.VENDING_MACHINE;
            after["extradata"] = root.ToJsonString();
            return proposed with { After = after.ToJsonString(), Animates = animates };
        }
        catch (Exception ex)
            when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            return proposed with
            {
                Reason = "Extra data could not be read safely. Correct it in Behaviour first.",
            };
        }
    }
}
