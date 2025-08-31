using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Turbo.Core.Plugins;

public interface ITurboPlugin
{
    string PluginName { get; }
    string PluginAuthor { get; }

    void ConfigureHost(IHostBuilder host);

    void ConfigureServices(HostBuilderContext context, IServiceCollection services);
}
