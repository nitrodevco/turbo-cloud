namespace Turbo.Admin.Api.Contracts;

/// <summary>How many logins reached a step (ClientLoginSteps) and how long after the socket opened.</summary>
public sealed record ClientLoginStepFigure(string Step, int Sessions, int P50Ms, int P95Ms);
