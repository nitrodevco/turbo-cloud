namespace Turbo.Primitives.Players.Permissions;

/// <summary>The meta keys core reads, registered by <see cref="CorePermissionNodeSource"/>.</summary>
public static class PermissionMetaKeys
{
    public static class Client
    {
        /// <summary>
        /// A floor under the security level derived from a player's nodes, for a group that
        /// wants the client's staff UI without any server power.
        /// </summary>
        public const string SECURITY_LEVEL = "client.security_level";
    }
}
