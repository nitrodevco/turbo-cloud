namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A built gamedata file to send: what it is, and its content gzipped.</summary>
public sealed record GamedataFileContent(GamedataFileSnapshot File, byte[] Gzipped);
