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
using Turbo.Web.Configuration;

namespace Turbo.Web.Api;

/// <summary>
/// The public site's API, its own small web host beside the silo like the admin API, on its own
/// address: the public domain's <c>/api</c> is sent here and reaches nothing of the panel. The site
/// is served from the same domain, so there are no cross-origin requests to allow.
/// </summary>
internal sealed class WebApiServer(
    IServiceProvider services,
    IOptions<WebConfig> config,
    ILoggerFactory loggerFactory,
    ILogger<WebApiServer> logger
) : IHostedService, IAsyncDisposable
{
    public const string SIGN_IN_POLICY = "web-sign-in";

    private WebApplication? _app;

    public async Task StartAsync(CancellationToken ct)
    {
        var options = config.Value;

        if (!options.Enabled)
            return;

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());

        builder.WebHost.UseKestrelCore().UseUrls(options.Url);
        builder.Services.AddRoutingCore();
        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(
                SIGN_IN_POLICY,
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
        builder.Services.AddSingleton(loggerFactory);
        builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        var app = builder.Build();

        // Behind the site's reverse proxy every request comes from loopback; its X-Forwarded-For
        // gives the real address the sign-in limit counts by.
        app.UseForwardedHeaders(
            new ForwardedHeadersOptions
            {
                ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            }
        );
        app.UseRateLimiter();
        app.Use(HandleFailureAsync);

        var api = app.MapGroup("/api");

        ActivatorUtilities.CreateInstance<DiscordEndpoints>(services).Map(api);
        ActivatorUtilities.CreateInstance<AccountEndpoints>(services).Map(api);

        try
        {
            await app.StartAsync(ct).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            logger.LogError(
                ex,
                "Public site API could not listen on {Url}, so the site is unavailable; the hotel runs on without it. Free the port or set Turbo:Web:Url, then restart.",
                options.Url
            );

            await app.DisposeAsync().ConfigureAwait(false);

            return;
        }

        _app = app;

        logger.LogInformation(
            "Public site API listening on {Url} for {SiteUrl}{Discord}",
            options.Url,
            options.SiteUrl,
            options.Discord.IsConfigured
                ? string.Empty
                : " (Discord sign-in not set up: Turbo:Web:Discord)"
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

    /// <summary>A failure is logged with the request; the browser gets a bare 500, never the details.</summary>
    private async Task HandleFailureAsync(HttpContext http, RequestDelegate next)
    {
        try
        {
            await next(http).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            // The browser went away; nothing failed.
        }
        catch (Exception ex)
            when (!http.Response.HasStarted && !http.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "Public site API request {Method} {Path} failed",
                http.Request.Method,
                http.Request.Path
            );

            http.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
    }
}
