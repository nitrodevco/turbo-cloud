using System;
using System.Collections.Generic;

namespace Turbo.Core.Authorization;

public sealed class AuthorizationResult
{
    public static readonly AuthorizationResult Success = new(true, Array.Empty<Failure>());
    public bool Succeeded { get; }
    public IReadOnlyList<Failure> Failures { get; }

    private AuthorizationResult(bool ok, IReadOnlyList<Failure> fails)
    {
        Succeeded = ok;
        Failures = fails;
    }

    public static AuthorizationResult Fail(params Failure[] failures) =>
        new(
            false,
            failures.Length > 0
                ? failures
                : new[] { new Failure("Unknown", "Authorization failed.") }
        );

    public static AuthorizationResult From(bool ok, params Failure[] failures) =>
        ok ? Success : Fail(failures);
}
