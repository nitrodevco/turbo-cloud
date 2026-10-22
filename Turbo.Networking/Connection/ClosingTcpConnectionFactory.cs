using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using SuperSocket.Connection;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Connections;
using SuperSocket.Server.Connection;

namespace Turbo.Networking.Connection;

/// <summary>
/// Builds the connection factory every listener uses: SuperSocket's own, except that a plain
/// TCP or WebSocket connection is a <see cref="ClosingTcpPipeConnection"/>.
/// </summary>
internal sealed class ClosingConnectionFactoryBuilder(
    SocketOptionsSetter socketOptionsSetter,
    IConnectionStreamInitializersFactory connectionStreamInitializersFactory
) : ConnectionFactoryBuilder(socketOptionsSetter, connectionStreamInitializersFactory)
{
    public override IConnectionFactory Build(
        ListenOptions listenOptions,
        ConnectionOptions connectionOptions
    ) =>
        new ClosingTcpConnectionFactory(
            listenOptions,
            connectionOptions,
            SocketOptionsSetter,
            ConnectionStreamInitializersFactory
        );
}

/// <summary>
/// SuperSocket's <see cref="TcpConnectionFactory"/> with its plain socket connections made by
/// <see cref="ClosingTcpPipeConnection"/>. A listener with stream initializers (TLS) is left to
/// the base factory.
/// </summary>
/// <remarks>
/// The socket's kernel receive buffer is left to the operating system. SuperSocket sets it to
/// <see cref="ConnectionOptions.ReceiveBufferSize"/>, which is also how much it reads per receive
/// (4 KiB unless configured). On Linux a receive buffer set by hand turns off its automatic
/// sizing and caps the TCP window at about twice that, so a client's 256 KiB packet took about
/// five seconds to arrive even over loopback (Windows still answered it in milliseconds).
/// The read size still comes from <c>receiveBufferSize</c>; the kernel buffer from the system's
/// settings (on Linux <c>net.ipv4.tcp_rmem</c>).
/// </remarks>
internal sealed class ClosingTcpConnectionFactory(
    ListenOptions listenOptions,
    ConnectionOptions connectionOptions,
    Action<Socket> socketOptionsSetter,
    IConnectionStreamInitializersFactory connectionStreamInitializersFactory
)
    : TcpConnectionFactory(
        listenOptions,
        connectionOptions,
        socketOptionsSetter,
        connectionStreamInitializersFactory
    )
{
    // The base factory's pool is private; the same size it defaults to.
    private const int SOCKET_SENDER_POOL_SIZE = 1000;

    private readonly ObjectPool<SocketSender> _socketSenderPool =
        new DefaultObjectPool<SocketSender>(
            new DefaultPooledObjectPolicy<SocketSender>(),
            SOCKET_SENDER_POOL_SIZE
        );

    public override Task<IConnection> CreateConnection(
        object connection,
        CancellationToken cancellationToken
    )
    {
        if (ConnectionStreamInitializers?.Any() == true)
            return base.CreateConnection(connection, cancellationToken);

        var socket = (Socket)connection;

        ApplySocketOptions(socket);

        return Task.FromResult<IConnection>(
            new ClosingTcpPipeConnection(socket, ConnectionOptions, _socketSenderPool)
        );
    }

    // The base's options, in its order, except the receive buffer size (see the remarks).
    protected override void ApplySocketOptions(Socket socket)
    {
        TryApply("NoDelay", () => socket.NoDelay = true, ListenOptions.NoDelay);
        TryApply(
            "SendBufferSize",
            () => socket.SendBufferSize = ConnectionOptions.SendBufferSize,
            ConnectionOptions.SendBufferSize > 0
        );
        TryApply(
            "ReceiveTimeout",
            () => socket.ReceiveTimeout = ConnectionOptions.ReceiveTimeout,
            ConnectionOptions.ReceiveTimeout > 0
        );
        TryApply(
            "SendTimeout",
            () => socket.SendTimeout = ConnectionOptions.SendTimeout,
            ConnectionOptions.SendTimeout > 0
        );
        TryApply("the socket options setter", () => SocketOptionsSetter?.Invoke(socket), true);
    }

    private void TryApply(string option, Action apply, bool configured)
    {
        if (!configured)
            return;

        try
        {
            apply();
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to set {Option} for the socket", option);
        }
    }
}

/// <summary>
/// A TCP connection whose close always reports itself closed.
/// </summary>
/// <remarks>
/// When a connection ends, SuperSocket closes its socket and then raises <c>Closed</c>, which is
/// what removes the session and takes its player offline. Both run in one try block, and the
/// socket's <c>Shutdown</c> throws when the client has already reset the connection: a client
/// that leaves (a reload, a closed tab) while the server is still sending to it. The exception
/// is one SuperSocket ignores, so nothing was logged, <c>Closed</c> was never raised and the
/// connection could not be closed again: the session stayed in the gateway, its player online,
/// until the heartbeat removed it. Here a failed shutdown no longer skips the close event.
/// </remarks>
internal sealed class ClosingTcpPipeConnection(
    Socket socket,
    ConnectionOptions options,
    ObjectPool<SocketSender> socketSenderPool
) : TcpPipeConnection(socket, options, socketSenderPool)
{
    protected override void Close()
    {
        try
        {
            base.Close();
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            // The socket is closed either way: the base closes it in a finally after Shutdown.
            Logger?.LogDebug(ex, "Shutting down a socket the client had already left failed");
        }
    }
}
