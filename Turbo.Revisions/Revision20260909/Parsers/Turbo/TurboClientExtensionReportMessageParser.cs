using System;
using System.Collections.Immutable;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Plugins;

namespace Turbo.Revisions.Revision20260909.Parsers.Turbo;

internal class TurboClientExtensionReportMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var count = Math.Clamp(packet.PopInt(), 0, TurboClientExtensionReportMessage.MAX_REPORTS);
        var reports = ImmutableArray.CreateBuilder<ClientExtensionReport>(count);

        for (var i = 0; i < count; i++)
            reports.Add(
                new ClientExtensionReport(
                    Owner: packet.PopString(),
                    Kind: packet.PopString(),
                    Subject: packet.PopString(),
                    Reason: packet.PopString(),
                    Message: packet.PopString()
                )
            );

        return new TurboClientExtensionReportMessage { Reports = reports.ToImmutable() };
    }
}
