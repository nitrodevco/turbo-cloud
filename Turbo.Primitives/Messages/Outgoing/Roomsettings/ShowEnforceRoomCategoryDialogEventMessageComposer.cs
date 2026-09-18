using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

[GenerateSerializer, Immutable]
public sealed record ShowEnforceRoomCategoryDialogEventMessageComposer : IComposer
{
    [Id(0)]
    public required int SelectionType { get; init; }
}
