using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Orleans;
using Turbo.Admin.Auth;
using Turbo.Admin.Auth.Endpoints;
using Turbo.Admin.Configuration;
using Turbo.Admin.Rooms.Endpoints;
using Turbo.Admin.Terminal;
using Turbo.Admin.Users.Endpoints;
using Turbo.Database.Context;

namespace Turbo.Admin.Hosting;

internal sealed class AdminWebHost(
    IOptions<AdminConfig> config,
    IGrainFactory grainFactory,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IAdminAccountService accountService,
    IJwtTokenService tokenService,
    IPasswordHasher passwordHasher,
    ConsoleBroadcastService consoleBroadcastService,
    IConsoleCommandExecutor consoleCommandExecutor,
    ILoggerFactory loggerFactory,
    ILogger<AdminWebHost> logger
) : IAdminWebHost
{
    private readonly AdminConfig _config = config.Value;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IAdminAccountService _accountService = accountService;
    private readonly IJwtTokenService _tokenService = tokenService;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ConsoleBroadcastService _consoleBroadcastService = consoleBroadcastService;
    private readonly IConsoleCommandExecutor _consoleCommandExecutor = consoleCommandExecutor;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly ILogger<AdminWebHost> _logger = logger;

    private const string DEV_CORS_POLICY = "turbo-admin-dev";

    private WebApplication? _app;
    private readonly object _gate = new();

    public async Task StartAsync(CancellationToken ct)
    {
        WebApplication app;

        lock (_gate)
        {
            if (_app is not null)
                return;

            app = Build();
            _app = app;
        }

        await app.StartAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Admin panel listening on port {Port}", _config.Port);
    }

    public async Task StopAsync()
    {
        WebApplication? app;

        lock (_gate)
        {
            app = _app;
            _app = null;
        }

        if (app is not null)
            await app.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    private WebApplication Build()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls($"http://0.0.0.0:{_config.Port}");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(_loggerFactory);

        builder.Services.AddSingleton(_grainFactory);
        builder.Services.AddSingleton(_dbCtxFactory);
        builder.Services.AddSingleton(_accountService);
        builder.Services.AddSingleton(_tokenService);
        builder.Services.AddSingleton(_passwordHasher);
        builder.Services.AddSingleton(_consoleBroadcastService);
        builder.Services.AddSingleton<IConsoleBroadcaster>(_consoleBroadcastService);
        builder.Services.AddSingleton(_consoleCommandExecutor);
        builder.Services.AddHostedService<ConsoleBroadcastBridge>();

        builder.Services.AddSignalR();
        builder.Services.AddCors(options =>
            options.AddPolicy(
                DEV_CORS_POLICY,
                policy =>
                    policy
                        .WithOrigins([.. _config.AllowedDevOrigins])
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
            )
        );

        var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.Jwt.Secret));

        builder
            .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _config.Jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _config.Jwt.Issuer,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwtKey,
                    ValidateLifetime = true,
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        if (
                            !string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs")
                        )
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseCors(DEV_CORS_POLICY);
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapAdminAuthEndpoints();
        app.MapAdminUserEndpoints();
        app.MapAdminRoomEndpoints();
        app.MapHub<ConsoleHub>("/hubs/console");

        var webRoot = System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot");

        if (System.IO.Directory.Exists(webRoot))
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.MapFallbackToFile("index.html");
        }

        return app;
    }
}
