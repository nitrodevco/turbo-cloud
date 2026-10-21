namespace Turbo.Admin.Api.Contracts;

/// <summary>A step of a funnel: the players who logged this event and every step before it, in the range.</summary>
public sealed record ClientFunnelStep(string Category, string Type, string Action, int Players);
