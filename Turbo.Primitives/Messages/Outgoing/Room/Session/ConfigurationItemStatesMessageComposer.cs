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
    /// <summary>
    /// A Handitem blocker (<c>conf_handitem_block</c>) is switched on: clients stop offering to
    /// give or drop a hand item.
    /// </summary>
    [Id(0)]
    public required bool IsHanditemControlBlocked { get; init; }

    /// <summary>
    /// A <c>:chooser</c> disabler (<c>conf_chooser_disable</c>) is switched on: clients open the
    /// user chooser only for room rights holders.
    /// </summary>
    [Id(1)]
    public required bool ChooserDisabled { get; init; }

    /// <summary>
    /// Free furni movement: anyone may move furni. No furni of this hotel's switches it, so it
    /// always goes out off.
    /// </summary>
    [Id(2)]
    public required bool FreeFurniMovementsEnabled { get; init; }

    /// <summary>
    /// An Invisible Furni Controller (<c>conf_invis_control</c>) is switched on: clients hide the
    /// furni layers tagged <c>invisible</c> (<c>RoomEngine.setInvisibleFurni</c>).
    /// </summary>
    [Id(3)]
    public required bool InvisibleFurni { get; init; }
}
