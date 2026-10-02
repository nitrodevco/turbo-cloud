using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Availability;

[GenerateSerializer, Immutable]
public sealed record MaintenanceStatusMessageComposer : IComposer
{
    [Id(0)]
    public required bool IsInMaintenance { get; init; }

    [Id(1)]
    public required int MinutesUntilMaintenance { get; init; }

    /// <summary>How long the maintenance lasts, in minutes; 0 when it has no set end.</summary>
    [Id(2)]
    public int DurationMinutes { get; init; }
}
