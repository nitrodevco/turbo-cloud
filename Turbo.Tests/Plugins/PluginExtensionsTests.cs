using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Networking.Extensions;
using Turbo.Plugins;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Packets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// A plugin reaches the extension packet registry through its <see cref="HostServices"/>, which
/// notes what it registers, so the admin panel can say which plugin owns a capability or header;
/// what the plugin disposes is forgotten there too, and the host's registry is the one changed.
/// </summary>
public sealed class PluginExtensionsTests
{
    [Fact]
    public void What_a_plugin_registers_is_noted_against_it_until_it_disposes_it()
    {
        var host = new ExtensionPacketRegistry();
        var extensions = new PluginExtensions();
        var services = new HostServices(
            new ServiceCollection()
                .AddSingleton<IExtensionPacketRegistry>(host)
                .BuildServiceProvider(),
            "plugin",
            extensions
        );
        var fakes = new Fakes();
        fakes.Handlers["get_Header"] = _ => 20002;

        var registry = services.GetRequiredService<IExtensionPacketRegistry>();
        var capability = registry.RegisterCapability("sample");
        var parser = registry.RegisterParser(20001, (IParser)fakes.Create(typeof(IParser)));
        var serializer = registry.RegisterSerializer(
            typeof(string),
            (ISerializer)fakes.Create(typeof(ISerializer))
        );

        extensions.Capabilities.Should().Equal("sample");
        extensions.IncomingHeaders.Should().Equal(20001);
        extensions.OutgoingHeaders.Should().Equal(20002);
        host.HasCapability("sample").Should().BeTrue();

        capability.Dispose();
        parser.Dispose();
        serializer.Dispose();

        extensions.Capabilities.Should().BeEmpty();
        extensions.IncomingHeaders.Should().BeEmpty();
        extensions.OutgoingHeaders.Should().BeEmpty();
        host.HasCapability("sample").Should().BeFalse();
        host.TryGetParser(20001, out _).Should().BeFalse();
    }
}
