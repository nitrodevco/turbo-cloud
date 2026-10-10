namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>Whether a publish target could be reached and its folder listed, and what happened in words.</summary>
public sealed record AssetPublishTestResult(bool Ok, string Message);
