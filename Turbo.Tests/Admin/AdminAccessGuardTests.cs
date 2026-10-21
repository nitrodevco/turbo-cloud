using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Admin.Api;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Every admin route says who may call it, as every packet handler that needs a permission
/// carries <c>[RequiresPermission]</c> (<c>PermissionGateTests</c>): its group's or its own
/// <see cref="AdminAccess"/>, or a note saying why the panel alone is enough. Each route used to
/// check in its handler, and a route whose author forgot was open to anyone with
/// <c>admin.panel</c>; a change (anything but a GET) also has to say what changing needs.
/// </summary>
public sealed class AdminAccessGuardTests : IAsyncDisposable
{
    private readonly IHostedService _server;
    private readonly int _port = TestPorts.Free();

    public AdminAccessGuardTests()
    {
        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            new Fakes(),
            NullLoggerFactory.Instance
        );
    }

    public async ValueTask DisposeAsync() => await _server.StopAsync(CancellationToken.None);

    [Fact]
    public async Task Every_route_behind_the_session_declares_who_may_call_it()
    {
        var routes = await RoutesAsync();

        routes.Should().HaveCountGreaterThan(300, "the panel's routes were all found");
        string.Join(
                ", ",
                routes
                    .Where(x => !x.Notes.Any(n => n.Kind == AdminAccessNoteKind.PanelOnly))
                    .Where(x => !x.Access.Any(a => !a.ChangesOnly))
                    .Select(x => x.Name)
            )
            .Should()
            .BeEmpty("a route needs a node to be seen, or says the panel alone is enough");
    }

    [Fact]
    public async Task Every_route_that_changes_something_declares_what_changing_needs()
    {
        var routes = await RoutesAsync();

        string.Join(
                ", ",
                routes
                    .Where(x => x.Changes)
                    .Where(x => x.Notes.Count == 0)
                    .Where(x => !x.Access.Any(a => a.ChangesOnly || a.OnRoute))
                    .Select(x => x.Name)
            )
            .Should()
            .BeEmpty(
                "a change needs its group's RequirePermissionToChange, a node of its own, or a note on why it needs no more"
            );
    }

    [Fact]
    public async Task A_note_gives_its_reason_and_a_panel_only_route_asks_for_no_node()
    {
        var routes = await RoutesAsync();

        string.Join(
                ", ",
                routes
                    .Where(x => x.Notes.Any(n => string.IsNullOrWhiteSpace(n.Reason)))
                    .Select(x => x.Name)
            )
            .Should()
            .BeEmpty();
        string.Join(
                ", ",
                routes
                    .Where(x => x.Notes.Any(n => n.Kind == AdminAccessNoteKind.PanelOnly))
                    .Where(x => x.Access.Count > 0)
                    .Select(x => x.Name)
            )
            .Should()
            .BeEmpty();
    }

    private sealed record Route(
        string Name,
        bool Changes,
        IReadOnlyList<AdminAccess> Access,
        IReadOnlyList<AdminAccessNote> Notes
    );

    private async Task<List<Route>> RoutesAsync()
    {
        await _server.StartAsync(TestContext.Current.CancellationToken);

        var app = (IEndpointRouteBuilder)
            _server
                .GetType()
                .GetField("_app", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(_server)!;

        return
        [
            .. app
                .DataSources.SelectMany(x => x.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(x => x.Metadata.GetMetadata<AdminSessionRequired>() is not null)
                .SelectMany(x =>
                    (x.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"]).Select(
                        method => new Route(
                            $"{method} {x.RoutePattern.RawText}",
                            !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method),
                            x.Metadata.GetOrderedMetadata<AdminAccess>(),
                            x.Metadata.GetOrderedMetadata<AdminAccessNote>()
                        )
                    )
                ),
        ];
    }
}
