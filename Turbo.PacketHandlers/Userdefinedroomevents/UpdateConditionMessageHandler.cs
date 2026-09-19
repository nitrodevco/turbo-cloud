using Orleans;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateConditionMessageHandler(IGrainFactory grainFactory)
    : UpdateWiredMessageHandler<UpdateConditionMessage>(grainFactory);
