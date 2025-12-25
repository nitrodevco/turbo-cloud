using System;
using System.Text.Json;
using Turbo.Furniture.WiredData;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.WiredData;

namespace Turbo.Furniture.Providers;

public sealed class WiredDataFactory : IWiredDataFactory
{
    public IWiredData CreateWiredData(WiredType type)
    {
        return type switch
        {
            WiredType.Action => new WiredActionData(),
            WiredType.Addon => new WiredAddonData(),
            WiredType.Condition => new WiredConditionData(),
            WiredType.Selector => new WiredSelectorData(),
            WiredType.Trigger => new WiredTriggerData(),
            WiredType.Variable => new WiredVariableData(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), "Unknown wired data type"),
        };
    }

    public IWiredData CreateWiredDataFromJson(WiredType type, string? jsonData)
    {
        if (!string.IsNullOrEmpty(jsonData))
        {
            var reader = new ExtraData(jsonData);

            return CreateWiredDataFromExtraData(type, reader);
        }

        return CreateWiredData(type);
    }

    public IWiredData CreateWiredDataFromExtraData(WiredType type, IExtraData extraData)
    {
        if (extraData.TryGetSection("wired", out var wiredElement))
        {
            return type switch
            {
                WiredType.Action => wiredElement.Deserialize<WiredActionData>()!,
                WiredType.Addon => wiredElement.Deserialize<WiredAddonData>()!,
                WiredType.Condition => wiredElement.Deserialize<WiredConditionData>()!,
                WiredType.Selector => wiredElement.Deserialize<WiredSelectorData>()!,
                WiredType.Trigger => wiredElement.Deserialize<WiredTriggerData>()!,
                WiredType.Variable => wiredElement.Deserialize<WiredVariableData>()!,
                _ => throw new ArgumentOutOfRangeException(nameof(type), "Unknown wired data type"),
            };
        }

        return CreateWiredData(type);
    }
}
