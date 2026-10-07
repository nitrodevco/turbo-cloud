namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A run of the chat log, newest first, with whether there are older and newer lines past it.
/// Paged by line rather than counted, so a long log is never read whole to show one page.
/// </summary>
public sealed record ChatlogResponse(
    int PageSize,
    bool HasOlder,
    bool HasNewer,
    ChatlogEntry[] Entries
);
