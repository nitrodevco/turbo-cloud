using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

/// <summary>
/// What the room's configuration furni switch on, for every client in it (Flash
/// <c>RoomMessageHandler.onConfigurationItemStates</c>). The client reads the first flag and then
/// each later one only while bytes are left, so all four always go out.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ConfigurationItemStatesMessageComposer : IComposer
{
    /// <summary>The Handitem blocker (<c>conf_handitem_block</c>), which this hotel does not run yet.</summary>
    [Id(0)]
    public required bool IsHanditemControlBlocked { get; init; }

    /// <summary>The <c>:chooser</c> disabler, which this hotel does not run yet.</summary>
    [Id(1)]
    public required bool ChooserDisabled { get; init; }

    /// <summary>Free furni movement, which this hotel does not run yet.</summary>
    [Id(2)]
    public required bool FreeFurniMovementsEnabled { get; init; }

    /// <summary>
    /// An Invisible Furni Controller (<c>conf_invis_control</c>) is switched on: clients hide the
    /// furni layers tagged <c>invisible</c> (<c>RoomEngine.setInvisibleFurni</c>).
    /// </summary>
    [Id(3)]
    public required bool InvisibleFurni { get; init; }
}
