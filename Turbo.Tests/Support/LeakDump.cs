using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Turbo.Tests.Support;

/// <summary>
/// Writes a heap dump of the test process when a plugin context did not unload and
/// <c>TURBO_TEST_LEAK_DUMPS</c> names a folder (needs the dotnet-dump tool): what holds the
/// context is read from the dump with <c>gcroot</c>. Does nothing otherwise.
/// </summary>
public static class LeakDump
{
    public static void IfHeld(IEnumerable<LogEntry> entries, string label)
    {
        var folder = Environment.GetEnvironmentVariable("TURBO_TEST_LEAK_DUMPS");

        if (
            string.IsNullOrEmpty(folder)
            || !entries.Any(x =>
                x.Level >= LogLevel.Warning && x.Message.Contains("did not unload")
            )
        )
            return;

        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"{label}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.dmp");
        using var dump = Process.Start(
            new ProcessStartInfo(
                "dotnet-dump",
                $"collect -p {Environment.ProcessId} --type Heap -o \"{path}\""
            )
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        );

        dump?.WaitForExit(120_000);
    }
}
