using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Settings.Snapshots;

/// <summary>A page of the settings' history, newest first.</summary>
[GenerateSerializer, Immutable]
public sealed record ServerSettingHistoryPage
{
    [Id(0)]
    public required ImmutableArray<ServerSettingChangeSnapshot> Items { get; init; }

    [Id(1)]
    public required int Total { get; init; }

    [Id(2)]
    public required int PageSize { get; init; }
}
