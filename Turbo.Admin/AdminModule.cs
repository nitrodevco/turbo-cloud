using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Turbo.Admin.Api;
using Turbo.Admin.Assets;
using Turbo.Admin.Catalog;
using Turbo.Admin.Commands;
using Turbo.Admin.Configuration;
using Turbo.Admin.Content;
using Turbo.Admin.Links;
using Turbo.Admin.Live;
using Turbo.Admin.Notifications;
using Turbo.Admin.Performance;
using Turbo.Admin.Permissions;
using Turbo.Admin.Players;
using Turbo.Admin.Rooms;
using Turbo.Admin.Search;
using Turbo.Contracts.Plugins;

namespace Turbo.Admin;

/// <summary>
/// The admin panel's server side: an HTTP API beside the silo, sign-in by passkey only (made from
/// a setup link, <c>adminsetup</c>), and a remote command console that runs operator commands as
/// the signed-in player. The panel itself is a separate React app. Off unless
/// <c>Turbo:Admin:Enabled</c>.
/// </summary>
public sealed class AdminModule : IHostPluginModule
{
    public string Key => "turbo-admin";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<AdminConfig>(builder.Configuration.GetSection(AdminConfig.SECTION_NAME));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AdminLinkPolicy>();
        services.AddSingleton<AdminRoomQueries>();
        services.AddSingleton<AdminRoomVisits>();
        services.AddSingleton<AdminRoomEditor>();
        services.AddSingleton<AdminPlayerQueries>();
        services.AddSingleton<AdminTicketPolicy>();
        services.AddSingleton<AdminSiteAccounts>();
        services.AddSingleton<AdminCommandLogQueries>();
        services.AddSingleton<AdminChatlogQueries>();
        services.AddSingleton<AdminCatalogQueries>();
        services.AddSingleton<AdminCatalogBuilder>();
        services.AddSingleton<AdminCatalogAudit>();
        services.AddSingleton<AdminBadgeQueries>();
        services.AddSingleton<AdminSearchQueries>();
        services.AddSingleton<AdminNotificationQueries>();
        services.AddSingleton<ClientAssets>();
        services.AddSingleton<PermissionViews>();
        services.AddSingleton<AdminLiveFeed>();
        services.AddHostedService(sp => sp.GetRequiredService<AdminLiveFeed>());
        services.AddSingleton<AdminPerformanceRecorder>();
        services.AddHostedService(sp => sp.GetRequiredService<AdminPerformanceRecorder>());
        services.AddHostedService<AdminApiServer>();
    }
}
