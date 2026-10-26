using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Policies;

/// <summary>
/// A policy over avatar effects in rooms: which effect an avatar wears when the hotel puts one on
/// it (the player's own from their inventory, an effect tile, a wired effect, a game team, riding,
/// a freeze), and which one each player in the room is shown on it. Either may let the effect
/// stand, replace it with another, or answer 0 for none. Taking an effect off is never asked. A
/// public class implementing it is found in a plugin's assembly as it loads and taken away as it
/// unloads; see <see cref="IGamePolicies"/> for the order several run in. With none, every avatar
/// wears what the hotel puts on it and everyone sees it.
/// <para>
/// Both run on the room's turn, <see cref="Show"/> once per player in the room for each change, so
/// they answer at once from what the plugin holds in memory (a set of players who turned effects
/// off, kept from its own command and table), never from the database or another grain. A policy
/// that throws is logged and passed over: the effect stands.
/// </para>
/// <para>
/// <see cref="Wear"/> changes what the avatar has on in the room: wired conditions and the room's
/// own clean-up read it. Replacing an effect with another leaves the clean-up to the plugin: the
/// hotel takes off what it put on (leaving an effect tile, getting off a horse), so the
/// replacement stays on until something takes it off. Answering 0 needs nothing more.
/// <see cref="Show"/> changes only what one player's client is sent.
/// </para>
/// </summary>
public interface IAvatarEffectPolicy
{
    /// <summary>Lower runs first. 0 by default.</summary>
    int Order => 0;

    /// <param name="proposed">
    /// What the policies before this one decided, the effect the hotel puts on at the start.
    /// </param>
    /// <returns>The effect the avatar wears: <paramref name="proposed"/>, another one, or 0 for none.</returns>
    int Wear(AvatarEffectRequest request, int proposed) => proposed;

    /// <param name="shown">
    /// What the policies before this one decided, the effect the avatar wears at the start (0 when
    /// it is taken off).
    /// </param>
    /// <returns>The effect the viewer sees: <paramref name="shown"/>, another one, or 0 for none.</returns>
    int Show(AvatarEffectView view, int shown) => shown;
}

/// <summary>
/// An effect the hotel puts on an avatar in a room: the avatar (its kind, and its player when it
/// is a player's) and the effect.
/// </summary>
public readonly record struct AvatarEffectRequest(
    RoomId RoomId,
    RoomObjectId ObjectId,
    RoomObjectType AvatarType,
    PlayerId? PlayerId,
    int EffectId
);

/// <summary>
/// The effect an avatar wears, as one player in the room (<paramref name="ViewerId"/>, the
/// avatar's own player included) is about to be sent it.
/// </summary>
public readonly record struct AvatarEffectView(
    RoomId RoomId,
    PlayerId ViewerId,
    RoomObjectId ObjectId,
    RoomObjectType AvatarType,
    PlayerId? PlayerId,
    int EffectId
);
