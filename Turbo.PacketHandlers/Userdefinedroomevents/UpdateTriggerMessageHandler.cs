using Orleans;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateTriggerMessageHandler(IGrainFactory grainFactory)
    : UpdateWiredMessageHandler<UpdateTriggerMessage>(grainFactory)
{
    // Official client capture 2026-10-24: a 21-pick save (wired matrix trigger.4.E2).
    protected override int ErrorReportMessageId => 3050;
}
