namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The lines said around one line in its room, newest first, with whether its room said more
/// before and after them.
/// </summary>
public sealed record ChatlogContextResponse(bool HasOlder, bool HasNewer, ChatlogEntry[] Entries);
