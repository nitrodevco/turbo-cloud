using FluentAssertions;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Plugins;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel lists a plugin's data by the kind the uninstall dry run names: each kind is its own,
/// so the texts and variables a plugin brought are never shown as permission grants.
/// </summary>
public sealed class PluginDataItemViewTests
{
    [Theory]
    [InlineData(PluginDataKind.Table, "table")]
    [InlineData(PluginDataKind.Setting, "setting")]
    [InlineData(PluginDataKind.Grant, "grant")]
    [InlineData(PluginDataKind.Variable, "variable")]
    [InlineData(PluginDataKind.Text, "text")]
    public void Each_kind_is_named_as_itself(PluginDataKind kind, string name) =>
        PluginDataItemView
            .From(new PluginDataItem(kind, "hello.window.title", 1, "Hello"))
            .Should()
            .Be(new PluginDataItemView(name, "hello.window.title", 1, "Hello"));
}
