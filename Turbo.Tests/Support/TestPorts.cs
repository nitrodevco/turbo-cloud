using System.Net;
using System.Net.Sockets;

namespace Turbo.Tests.Support;

/// <summary>
/// A loopback port for a test server that binds it later. A port the OS picks for a listener on
/// port 0 is one of its dynamic ports: once that listener is closed, any outgoing connection (the
/// tests' own HTTP clients run in parallel) can be given it before the server binds it, and the
/// server then fails to start. These ports come from below every OS's dynamic range, each one only
/// once per test run, and only if nothing listens on it now.
/// </summary>
public static class TestPorts
{
    /// <summary>First port handed out (below the Windows 49152 and Linux 32768 dynamic ranges).</summary>
    public const int First = 21000;

    /// <summary>Last port handed out.</summary>
    public const int Last = 29999;

    private static int _next = First + (Environment.ProcessId % 1000) * 8 - 1;

    public static int Free()
    {
        for (var tries = 0; tries <= Last - First; tries++)
        {
            var port = First + (Interlocked.Increment(ref _next) - First) % (Last - First + 1);

            try
            {
                using var probe = new TcpListener(IPAddress.Loopback, port);
                probe.Start();
                return port;
            }
            catch (SocketException) { }
        }

        throw new InvalidOperationException($"no free loopback port in {First}-{Last}");
    }
}
