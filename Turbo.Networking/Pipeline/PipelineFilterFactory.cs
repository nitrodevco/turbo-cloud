using System;
using SuperSocket.ProtoBase;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;
using Turbo.Packets.Incoming;

namespace Turbo.Networking.Pipeline;

public class PipelineFilterFactory : PipelineFilterFactoryBase<IClientPacket>
{
    protected override IPipelineFilter<IClientPacket> Create() => new PipelineFilter();
}
