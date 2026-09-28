namespace Turbo.Primitives.Players.Permissions;

/// <summary>A meta key a group or player may carry a value for.</summary>
/// <param name="Key">The key, in the same dotted form as a node.</param>
/// <param name="Description">What the value means, for the console and the check trace.</param>
public sealed record PermissionMetaDefinition(string Key, string Description);
