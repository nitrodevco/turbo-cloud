using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Turbo.Web.Api.Contracts;
using Turbo.Web.Configuration;

namespace Turbo.Web.Api;

/// <summary>
/// <c>/version</c>: which build of the server answers, so a deploy can be checked against the
/// repository without signing in. The commit is the one the build stamped into the assembly's
/// informational version (<c>1.0.0+&lt;sha&gt;</c>, from the checkout, or
/// <c>-p:SourceRevisionId=&lt;sha&gt;</c> when published outside one). Off with
/// <c>Turbo:Web:ShowVersion</c>: then there is no such endpoint.
/// </summary>
internal sealed class VersionEndpoints(IOptions<WebConfig> config)
{
    public void Map(IEndpointRouteBuilder api)
    {
        if (!config.Value.ShowVersion)
            return;

        var version = Describe(
            typeof(VersionEndpoints)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
        );

        api.MapGet("/version", () => Results.Ok(version));
    }

    /// <summary><c>1.0.0+abc</c> is version 1.0.0 built from abc; one with no <c>+</c> names no commit.</summary>
    public static VersionResponse Describe(string? informationalVersion)
    {
        var value = informationalVersion ?? string.Empty;
        var plus = value.IndexOf('+');

        if (plus < 0)
            return new(value, null);

        var commit = value[(plus + 1)..];

        return new(value[..plus], commit.Length == 0 ? null : commit);
    }
}
