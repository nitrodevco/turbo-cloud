namespace Turbo.Core.Authorization;

public readonly record struct AuthorizationResult(bool Ok, Failure[] Fails);
