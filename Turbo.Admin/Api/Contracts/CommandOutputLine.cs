namespace Turbo.Admin.Api.Contracts;

/// <summary>One line a command answered: <c>reply</c> for a short status, <c>notice</c> for a report.</summary>
public sealed record CommandOutputLine(string Kind, string Text);
