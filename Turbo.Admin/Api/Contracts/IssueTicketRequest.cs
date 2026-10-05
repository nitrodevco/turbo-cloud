namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A login ticket to issue: how many minutes it works for (null for never), and whether it works
/// more than once.
/// </summary>
public sealed record IssueTicketRequest(int? LifetimeMinutes, bool Reusable);
