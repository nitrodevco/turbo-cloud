using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Turbo.Events.Abstractions;
using Turbo.Events.Abstractions.Registry;
using Turbo.Events.Configuration;
using Turbo.Pipeline.Abstractions.Attributes;
using Turbo.Pipeline.Core.Registry;

namespace Turbo.Events.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboEvents(
        this IServiceCollection services,
        IConfiguration cfg
    )
    {
        services.AddOptions<EventConfig>().Bind(cfg.GetSection(EventConfig.SECTION_NAME));

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<EventConfig>>().Value);

        services.AddSingleton<IEventBus, EventBus>();

        return services;
    }
}
