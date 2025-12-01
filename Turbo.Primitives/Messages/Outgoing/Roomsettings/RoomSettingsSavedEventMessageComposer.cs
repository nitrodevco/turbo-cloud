using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

[GenerateSerializer, Immutable]
public sealed record RoomSettingsSavedEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
