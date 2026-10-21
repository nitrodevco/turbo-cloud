namespace Turbo.Admin.Api.Contracts;

/// <summary>The keys of Habbo's variables to take in; none named takes every key waiting.</summary>
public sealed record HabboVariableTakeRequest(string[]? Keys);
