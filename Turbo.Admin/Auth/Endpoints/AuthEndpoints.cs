using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Turbo.Admin.Auth.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/auth");

        group.MapPost(
            "/login",
            async (
                LoginRequest request,
                IAdminAccountService accountService,
                IJwtTokenService tokenService,
                CancellationToken ct
            ) =>
            {
                var adminUser = await accountService
                    .ValidateCredentialsAsync(request.Username, request.Password, ct)
                    .ConfigureAwait(false);

                if (adminUser is null)
                    return Results.Unauthorized();

                var token = tokenService.IssueToken(adminUser);

                return Results.Ok(
                    new LoginResponse(token, adminUser.Username, adminUser.Role.ToString())
                );
            }
        );

        return app;
    }
}

internal sealed record LoginRequest(string Username, string Password);

internal sealed record LoginResponse(string Token, string Username, string Role);
