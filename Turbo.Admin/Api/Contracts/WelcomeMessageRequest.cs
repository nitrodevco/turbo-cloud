namespace Turbo.Admin.Api.Contracts;

/// <summary>The welcome message to show every player when they log in; empty turns it off.</summary>
public sealed record WelcomeMessageRequest(string? Message);
