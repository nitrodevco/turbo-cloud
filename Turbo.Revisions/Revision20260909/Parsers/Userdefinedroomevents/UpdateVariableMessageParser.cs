using System;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents;

internal class UpdateVariableMessageParser : UpdateWiredDataParser, IParser
{
    public override Type UpdateMessageType => typeof(UpdateVariableMessage);
}
