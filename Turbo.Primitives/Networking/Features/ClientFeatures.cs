using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Turbo.Primitives.Networking.Features;

/// <summary>
/// The features of Habbo's own protocol a client says it understands, as names at the end of
/// its <c>ClientHelloMessage</c>: each stands for newer packet layouts, and a client that sends
/// no list reads the older ones. They are Habbo's, and not the extensions Turbo offers a client
/// that asks (<see cref="Capabilities.ClientCapabilities"/>). See <c>docs/client-features.md</c>.
/// </summary>
public static class ClientFeatures
{
    public const string AVATAR_COLLECTIBLES = "avatar_collectibles";
    public const string PAID_NAME_CHANGE = "paid_name_change";
    public const string PET_INVENTORY_LOCATIONS = "pet_inventory_locations";
    public const string PRIVATE_MARKETPLACE_OFFERS = "private_marketplace_offers";

    /// <summary>
    /// A moodlight preset says whether furni keep their own lights: one more boolean a preset
    /// in <c>RoomDimmerPresetsMessageComposer</c>.
    /// </summary>
    public const string ROOM_DIMMER_LIGHTS = "room_dimmer_lights";

    /// <summary>
    /// The most names one hello is read for; the rest of a longer list is ignored. A protocol
    /// bound against a hostile count, not a tunable.
    /// </summary>
    public const int MAX_ADVERTISED = 64;

    /// <summary>The longest name kept from a hello; a longer one is dropped.</summary>
    public const int MAX_NAME_LENGTH = 64;

    /// <summary>Every feature name the server knows.</summary>
    public static readonly FrozenSet<string> KNOWN = FrozenSet.ToFrozenSet(
        [
            AVATAR_COLLECTIBLES,
            PAID_NAME_CHANGE,
            PET_INVENTORY_LOCATIONS,
            PRIVATE_MARKETPLACE_OFFERS,
            ROOM_DIMMER_LIGHTS,
        ],
        StringComparer.Ordinal
    );

    /// <summary>The names in <paramref name="advertised"/> the server knows; the rest are dropped.</summary>
    public static FrozenSet<string> Known(IEnumerable<string> advertised) =>
        advertised.Where(KNOWN.Contains).ToFrozenSet(StringComparer.Ordinal);
}
