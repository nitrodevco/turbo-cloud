using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// Whether a Variable Management change was made; the client only tells a failure
/// (<c>wiredmenu.variable_management_detail.notification.modification_failed</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredSetUserPermanentVariableResultMessageComposer : IComposer
{
    [Id(0)]
    public required bool Success { get; init; }
}
