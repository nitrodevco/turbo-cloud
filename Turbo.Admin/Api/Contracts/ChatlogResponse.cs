namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A page of the chat log in <see cref="Order"/>. <see cref="Total"/> stops at
/// <see cref="Turbo.Admin.Api.LogCounts.LIMIT"/>, with <see cref="TotalCapped"/> when there are
/// more, so a long log is never read whole to show one page.
/// </summary>
public sealed record ChatlogResponse(
    int Total,
    bool TotalCapped,
    int Page,
    int PageSize,
    ListOrder Order,
    ChatlogEntry[] Entries
);
