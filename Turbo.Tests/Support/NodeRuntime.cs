using System.ComponentModel;
using System.Diagnostics;
using Xunit;

namespace Turbo.Tests.Support;

/// <summary>
/// The camera renderer is a node script: its tests run it where node is installed and skip
/// elsewhere.
/// </summary>
public static class NodeRuntime
{
    /// <summary>Skips the test unless <c>node</c> starts.</summary>
    public static void Require()
    {
        try
        {
            Process.Run("node", ["--version"], silent: true);
        }
        catch (Win32Exception)
        {
            Assert.Skip("node is not installed");
        }
    }
}
