using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Orleans;
using Turbo.Admin.Auth;
using Turbo.Admin.Auth.Endpoints;
using Turbo.Admin.Common;
using Turbo.Admin.Configuration;
using Turbo.Admin.Emulator;
using Turbo.Admin.Emulator.Endpoints;
using Turbo.Admin.Rooms.Endpoints;
using Turbo.Admin.Supervisor.Orleans;
using Turbo.Admin.Terminal;
using Turbo.Admin.Users.Endpoints;
using Turbo.Database.Entities.Admin;
using Turbo.Database.Extensions;
using Turbo.Logging.Extensions;

namespace Turbo.Admin.Supervisor;

internal class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("create-admin", StringComparison.OrdinalIgnoreCase))
        {
            return await RunCreateAdminAsync(args).ConfigureAwait(false);
        }

        // Force-load the grain implementation assemblies so Orleans' client-side grain type
        // resolver can see them; nothing here calls into their types directly otherwise, so the
        // runtime would never load them lazily and grain reference creation would fail.
        _ = typeof(Turbo.Players.PlayerModule).Assembly;
        _ = typeof(Turbo.Rooms.RoomModule).Assembly;

        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddEnvironmentVariables(prefix: "TURBO__");

        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddTurboConsoleLogger();

        builder.Services.Configure<AdminConfig>(
            builder.Configuration.GetSection(AdminConfig.SECTION_NAME)
        );

        builder.Services.AddTurboDatabaseContext(builder.Configuration);

        builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
        builder.Services.AddSingleton<IAdminAccountService, AdminAccountService>();

        builder.Services.AddSingleton<ConsoleBroadcastService>();
        builder.Services.AddSingleton<IConsoleBroadcaster>(sp =>
            sp.GetRequiredService<ConsoleBroadcastService>()
        );
        builder.Services.AddHostedService<ConsoleBroadcastBridge>();

        builder.Services.AddSingleton<IEmulatorProcessSupervisor, EmulatorProcessSupervisor>();

        var adminConfig = builder
            .Configuration.GetSection(AdminConfig.SECTION_NAME)
            .Get<AdminConfig>();

        builder.Services.AddSingleton<OrleansConnectionState>();
        builder.Services.AddSingleton<GrainClientHolder>();
        builder.Services.AddScoped<IClusterClient>(sp =>
            sp.GetRequiredService<GrainClientHolder>().Require()
        );
        builder.Services.AddScoped<IGrainFactory>(sp => sp.GetRequiredService<IClusterClient>());
        builder.Services.AddHostedService<OrleansClientConnector>();

        builder.Services.AddSignalR();
        builder.Services.AddCors(options =>
            options.AddPolicy(
                "turbo-admin-dev",
                policy =>
                    policy
                        .WithOrigins([.. adminConfig?.AllowedDevOrigins ?? []])
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
            )
        );

        var jwtSecret = adminConfig?.Jwt.Secret ?? string.Empty;
        var jwtIssuer = adminConfig?.Jwt.Issuer ?? "turbo-admin";
        var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

        builder
            .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtIssuer,
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

        if (adminConfig is not null)
            builder.WebHost.UseUrls($"http://0.0.0.0:{adminConfig.Port}");

        var app = builder.Build();

        app.UseCors("turbo-admin-dev");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEmulatorAvailabilityHandling();

        app.MapAdminAuthEndpoints();
        app.MapAdminUserEndpoints();
        app.MapAdminRoomEndpoints();
        app.MapAdminEmulatorEndpoints();
        app.MapHub<ConsoleHub>("/hubs/console");

        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");

        if (Directory.Exists(webRoot))
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.MapFallbackToFile("index.html");
        }

        await app.RunAsync().ConfigureAwait(false);

        return 0;
    }

    private static async Task<int> RunCreateAdminAsync(string[] args)
    {
        if (args.Length < 3)
        {
            System.Console.WriteLine(
                "Usage: create-admin <username> <password> [administrator|moderator]"
            );
            return 1;
        }

        var role =
            args.Length >= 4 && args[3].Equals("moderator", StringComparison.OrdinalIgnoreCase)
                ? AdminRoleType.Moderator
                : AdminRoleType.Administrator;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables(prefix: "TURBO__")
            .Build();

        var services = new ServiceCollection();
        services.AddTurboDatabaseContext(configuration);
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IAdminAccountService, AdminAccountService>();
        services.AddLogging();

        await using var provider = services.BuildServiceProvider();
        var accountService = provider.GetRequiredService<IAdminAccountService>();

        var created = await accountService
            .CreateAdminAsync(args[1], args[2], role, default)
            .ConfigureAwait(false);

        System.Console.WriteLine(
            created
                ? $"Admin user '{args[1]}' created with role {role}."
                : $"Failed to create admin user '{args[1]}' (username may already exist)."
        );

        return created ? 0 : 1;
    }
}
