using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

[GenerateSerializer, Immutable]
public sealed record RaidProtectionSettingsResultMessageComposer : IComposer
{
    [Id(0)]
    public required RaidProtectionSaveResultSnapshot Result { get; init; }
}
