using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Wired;

/// <summary>
/// Which room units wired counts as users: players and bots. In Habbo a bot's line, a bot placed
/// and picked up and a bot starting to dance fire the user triggers with the bot triggering;
/// Users by Name selects bots; Teleport To, Move/Rotate User, Move User To Furni and Freeze act on
/// them; Chase chases them and Move Furni To User moves onto a bot's tile; Actor Direction reads a
/// bot's and Actor In Group fails for one (official client capture 2026-10-24, W1/W2/W3 lab with
/// bots "Frank" and "frank"). User Count In Room counts players only, and Kick User does nothing to
/// a bot (same capture). A pet is not one (inference: no pet was recorded).
/// </summary>
public static class WiredUsers
{
    public static bool Counts(IRoomAvatar avatar) => avatar is IRoomPlayer or IRoomBot;
}
