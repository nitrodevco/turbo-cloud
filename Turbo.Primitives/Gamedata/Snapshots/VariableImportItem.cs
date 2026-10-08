using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One variable an import adds or changes.</summary>
[GenerateSerializer, Immutable]
public sealed record VariableImportItem
{
    [Id(0)]
    public required string Key { get; init; }

    /// <summary><see cref="FurnitureImportAction.Add"/> or <see cref="FurnitureImportAction.Update"/>.</summary>
    [Id(1)]
    public required FurnitureImportAction Action { get; init; }

    /// <summary>The hotel's value as JSON; null for a variable being added.</summary>
    [Id(2)]
    public string? Current { get; init; }

    /// <summary>The imported value as JSON.</summary>
    [Id(3)]
    public required string Incoming { get; init; }
}
