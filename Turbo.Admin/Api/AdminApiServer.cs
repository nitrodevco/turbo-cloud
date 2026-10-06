using System;
using System.IO;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Configuration;

namespace Turbo.Admin.Api;

/// <summary>
/// The admin panel's HTTP API, hosted beside the silo the way the game sockets are
/// (<c>NetworkManager</c>): its own small web host, whose handlers are built from the silo's
/// service provider. So an endpoint reaches grains, the command runner and the session gateway
/// in process, as the game does, rather than through an Orleans client from outside.
/// </summary>
internal sealed class AdminApiServer(
    IServiceProvider services,
    IOptions<AdminConfig> config,
    ILoggerFactory loggerFactory,
    ILogger<AdminApiServer> logger
) : IHostedService, IAsyncDisposable
{
    private WebApplication? _app;

    public async Task StartAsync(CancellationToken ct)
    {
        var options = config.Value;

        if (!options.Enabled)
            return;

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());

        builder.WebHost.UseKestrelCore().UseUrls(options.Url);
        builder.Services.AddRoutingCore();
        builder.Services.AddCors(cors =>
            cors.AddDefaultPolicy(policy =>
                policy
                    .WithOrigins(options.PanelUrl.TrimEnd('/'))
                    .WithHeaders("Authorization", "Content-Type")
                    .WithMethods("GET", "POST", "PUT", "DELETE")
            )
        );
        // Signing in is open to anyone, so each address gets a few tries a minute.
        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(
                AdminResults.SIGN_IN_POLICY,
                http =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        http.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = options.SignInAttemptsPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        }
                    )
            );
        });
        // The web host logs through the hotel's own loggers, not a second set.
        builder.Services.AddSingleton(loggerFactory);
        builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        var app = builder.Build();

        // Behind the panel's reverse proxy every request comes from loopback; the proxy's
        // X-Forwarded-For gives the real address, and only a loopback proxy is believed.
        app.UseForwardedHeaders(
            new ForwardedHeadersOptions
            {
                ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            }
        );
        app.UseCors();
        app.UseRateLimiter();
        app.Use(HandleFailureAsync);

        var secured = app.MapGroup("/api")
            .AddEndpointFilter(
                new AdminSessionFilter(services.GetRequiredService<IGrainFactory>())
            );

        var passkeys = ActivatorUtilities.CreateInstance<AdminPasskeys>(services);

        ActivatorUtilities.CreateInstance<AuthEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<SignInEndpoints>(services, passkeys).Map(app);
        ActivatorUtilities.CreateInstance<AccountEndpoints>(services, passkeys).Map(secured);
        ActivatorUtilities.CreateInstance<StaffEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<RoomEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<PlayerEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<PlayerAccountEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<HotelEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<DashboardEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<LiveEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<CommandEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<CommandLogEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<CatalogEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<ClientAssetEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<PermissionGroupEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<PermissionPlayerEndpoints>(services).Map(secured);
        ActivatorUtilities.CreateInstance<PermissionLookupEndpoints>(services).Map(secured);

        // The panel is a convenience: an address it cannot have (the port taken, or not this
        // machine's) leaves the hotel running without it rather than taking the hotel down.
        try
        {
            await app.StartAsync(ct).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            logger.LogError(
                ex,
                "Admin API could not listen on {Url}, so the admin panel is unavailable; the hotel runs on without it. Free the port or set Turbo:Admin:Url, then restart.",
                options.Url
            );

            await app.DisposeAsync().ConfigureAwait(false);

            return;
        }

        _app = app;

        logger.LogInformation(
            "Admin API listening on {Url} for the panel at {PanelUrl}",
            options.Url,
            options.PanelUrl
        );
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_app is { } app)
            await app.StopAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is { } app)
            await app.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// A failure is logged here with the request, and the panel gets a bare 500: the exception
    /// can name tables, grains and players, and the browser is no place for it.
    /// </summary>
    private async Task HandleFailureAsync(HttpContext http, RequestDelegate next)
    {
        try
        {
            await next(http).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            // The browser went away (a page change, a refresh, a query it no longer needs): the
            // grain calls gave up with it. Nobody is waiting for an answer, and nothing failed.
        }
        catch (Exception ex)
            when (!http.Response.HasStarted && !http.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "Admin API request {Method} {Path} failed",
                http.Request.Method,
                http.Request.Path
            );

            http.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
    }
}
