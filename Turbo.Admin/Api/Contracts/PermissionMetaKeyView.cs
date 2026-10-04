namespace Turbo.Admin.Api.Contracts;

/// <summary>A registered meta key, and how a value is chosen when several sources set it.</summary>
public sealed record PermissionMetaKeyView(string Key, string Description, string Selection);
