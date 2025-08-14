using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Encryption;
using Turbo.Core.Networking.Packets;
using Turbo.Core.Networking.Session;
using Turbo.Networking.Extensions;

namespace Turbo.Networking.Session;

public class Session(
    IChannelHandlerContext channel,
    ILogger<Session> logger
    ) : ISession
{
    private readonly IChannelHandlerContext _channel = channel;
    private readonly ILogger<Session> _logger = logger;
    private readonly ConcurrentQueue<IClientPacket> _pendingReadMessages = new();

    public IRc4Service Rc4 { get; set; }
    public long LastPongTimestamp { get; private set; } = DateTimeOffset.Now.ToUnixTimeSeconds();

    public async Task DisposeAsync()
    {
        await _channel.CloseAsync();
    }

    public async Task Send(IComposer composer)
    {
        await Send(composer, false);
    }

    public async Task SendQueue(IComposer composer)
    {
        await Send(composer, true);
    }

    public void Flush()
    {
        _channel.Flush();
    }

    private bool TryAddMessage(IClientPacket messageEvent)
    {
        _pendingReadMessages.Enqueue(messageEvent);

        messageEvent.Content.Retain();

        return true;
    }

    public void OnMessageReceived(IClientPacket messageEvent)
    {
        if (!TryAddMessage(messageEvent))
        {
            messageEvent.Content.ReleaseAll();
        }
    }

    public async Task HandleDecodedMessages()
    {
        while (true)
        {
            if (_pendingReadMessages.IsEmpty)
            {
                break;
            }

            if (!_pendingReadMessages.TryDequeue(out var msg)) continue;

            /* try
            {
                if (Revision.Parsers.TryGetValue(msg.Header, out var parser))
                {
                    _logger.LogInformation($"\u001b[94mINCOMING[{msg.Header}] -> {{in:{parser.GetType().Name}}}{msg.ToString()}\u001b[0m");

                    await parser.HandleAsync(this, msg, _messageHub);
                }
                else
                {
                    _logger.LogWarning("\u001b[91mNo matching parser found for incoming message {}\u001b[0m", msg.Header);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message");
            }
            finally
            {
                msg.Content.Release();
            } */
        }
    }

    protected async Task Send(IComposer composer, bool queue)
    {
        if (!IsConnected()) return;

        /* if (Revision.Serializers.TryGetValue(composer.GetType(), out var serializer))
        {
            var packet = serializer.Serialize(_channel.Allocator.Buffer(), composer);

            _logger.LogInformation($"\u001b[92mOUTGOING[{packet.Header}] -> {{out:{composer.GetType().Name}}}{packet.ToString()}\u001b[0m");

            try
            {
                if (queue) await _channel.WriteAsync(packet);
                else await _channel.WriteAndFlushAsync(packet);
            }

            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
            }
        }
        else
        {
            _logger.LogWarning($"\u001b[91mNo matching serializer found for outgoing message {composer.GetType().Name}\u001b[0m");
        } */
    }

    public bool IsConnected()
    {
        return _channel.Channel.Open;
    }
}