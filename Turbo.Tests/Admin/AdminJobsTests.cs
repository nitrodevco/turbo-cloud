using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Admin.Configuration;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Long admin work outlasting its request, as a catalog reset on a big catalog does behind the
/// panel's proxy: the request answers 202 and where to ask, the work goes on, and asking there
/// gives what the request would have answered - to the staff member who started it only. Work
/// done within the wait is answered as before.
/// </summary>
public sealed class AdminJobsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private IHostedService _server = null!;
    private int _signedIn = 1;

    public AdminJobsTests()
    {
        _port = TestPorts.Free();

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = _signedIn,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync(CancellationToken.None);
    }

    private async Task StartAsync(int waitSeconds)
    {
        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance,
            new AdminConfig
            {
                Enabled = true,
                Url = $"http://127.0.0.1:{_port}",
                JobWaitSeconds = waitSeconds,
            }
        );
        await _server.StartAsync(Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}/api{path}");

        if (method == HttpMethod.Post)
            request.Content = JsonContent.Create(new { removeLimited = false });

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task AResetOutlastingItsRequest_IsAskedAfter_AndAnswersAsItsRequestWould()
    {
        var reset = new TaskCompletionSource<CatalogEditResult>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

#pragma warning disable VSTHRD003 // The reset is held open until the test completes it.
        _fakes.Handlers["ResetAsync"] = _ => reset.Task;
#pragma warning restore VSTHRD003
        await StartAsync(waitSeconds: 0);

        var started = await SendAsync(HttpMethod.Post, "/catalog/reset");

        started.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var poll = (await started.Content.ReadFromJsonAsync<JsonElement>(Ct))
            .GetProperty("poll")
            .GetString()!;
        var running = await (
            await SendAsync(HttpMethod.Get, poll)
        ).Content.ReadFromJsonAsync<JsonElement>(Ct);

        running.GetProperty("done").GetBoolean().Should().BeFalse();

        // Another staff member doesn't see it.
        _signedIn = 2;
        (await SendAsync(HttpMethod.Get, poll)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        _signedIn = 1;

        reset.SetResult(CatalogEditResult.Refused("The catalog is already empty."));

        JsonElement done;

        do
        {
            await Task.Delay(20, Ct);
            done = await (
                await SendAsync(HttpMethod.Get, poll)
            ).Content.ReadFromJsonAsync<JsonElement>(Ct);
        } while (!done.GetProperty("done").GetBoolean());

        done.GetProperty("answerStatus").GetInt32().Should().Be(409);
        done.GetProperty("answer")
            .GetProperty("message")
            .GetString()
            .Should()
            .Be("The catalog is already empty.");
    }

    [Fact]
    public async Task WorkDoneWithinTheWait_IsAnsweredAsBefore()
    {
        _fakes.Handlers["ResetAsync"] = _ =>
            Task.FromResult(CatalogEditResult.Refused("The catalog is already empty."));
        await StartAsync(waitSeconds: 5);

        var answered = await SendAsync(HttpMethod.Post, "/catalog/reset");

        answered.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await answered.Content.ReadFromJsonAsync<JsonElement>(Ct))
            .GetProperty("message")
            .GetString()
            .Should()
            .Be("The catalog is already empty.");
    }
}
