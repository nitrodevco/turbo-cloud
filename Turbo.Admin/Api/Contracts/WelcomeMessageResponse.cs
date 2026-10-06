namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The welcome message every player is shown when they log in, empty when there is none, and
/// the longest one that can be saved.
/// </summary>
public sealed record WelcomeMessageResponse(string Message, int MaxLength);
