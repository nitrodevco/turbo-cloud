using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Moderation;

namespace Turbo.Operations;

/// <summary>
/// Running the hotel: sanctions, the word filter, the maintenance and shutdown countdown, and the operator
/// commands (<c>docs/commands.md</c> section 4.1) that put them in staff's hands.
/// </summary>
public sealed class OperationsModule : IHostPluginModule
{
    public string Key => "turbo-operations";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<OperationsConfig>(
            builder.Configuration.GetSection(OperationsConfig.SECTION_NAME)
        );

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISanctionService, SanctionService>();
        services.AddSingleton<IWordFilter, WordFilter>();

        // One instance answers as the availability and runs as the countdown's background loop.
        services.AddSingleton<HotelAvailabilityService>();
        services.AddSingleton<IHotelAvailability>(sp =>
            sp.GetRequiredService<HotelAvailabilityService>()
        );
        services.AddHostedService(sp => sp.GetRequiredService<HotelAvailabilityService>());
    }
}
