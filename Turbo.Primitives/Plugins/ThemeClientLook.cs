namespace Turbo.Primitives.Plugins;

/// <summary>The preview accepted for a client's load and the revision it was last given.</summary>
public sealed record ThemeClientLook(string? Requested, string Revision);
