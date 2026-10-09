using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

[GenerateSerializer, Immutable]
public sealed record RaidProtectionSettingsMessageComposer : IComposer
{
    [Id(0)]
    public required RaidProtectionSettingsSnapshot Settings { get; init; }
}
