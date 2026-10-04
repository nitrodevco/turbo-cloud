namespace Turbo.Admin.Api.Contracts;

/// <summary>A group as other things name it: a parent, a membership.</summary>
public sealed record PermissionGroupRef(string Name, string DisplayName, int Weight);
