namespace Turbo.Primitives.Players.Permissions;

/// <summary>The meta keys core reads, registered by <see cref="CorePermissionNodeSource"/>.</summary>
public static class PermissionMetaKeys
{
    /// <summary>
    /// Per-player overrides of limits the hotel configures. The config option is the hotel default;
    /// a group or player that sets one of these replaces it, the highest value among them winning,
    /// so a VIP group can raise a limit without lowering anyone else's. Read with
    /// <see cref="PermissionMeta.ReadLimit"/>.
    /// </summary>
    public static class Limit
    {
        /// <summary>Friends a player may have (<c>PlayerConfig.MessengerNormalFriendLimit</c>).</summary>
        public const string FRIENDS = "limit.friends";

        /// <summary>Rooms a player may own (<c>NavigatorConfig.MaxRoomsPerPlayer</c>).</summary>
        public const string ROOMS = "limit.rooms";

        /// <summary>Favourite rooms a player may keep (<c>PlayerNavigatorConfig.MaxFavouriteRooms</c>).</summary>
        public const string FAVOURITE_ROOMS = "limit.favourite_rooms";
    }

    public static class Client
    {
        /// <summary>
        /// A floor under the security level derived from a player's nodes, for a group that
        /// wants the client's staff UI without any server power.
        /// </summary>
        public const string SECURITY_LEVEL = "client.security_level";
    }
}
