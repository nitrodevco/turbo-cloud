using System;
using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Parsers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260112.Parsers.Userdefinedroomevents;

internal class UpdateConditionMessageParser : UpdateWiredDataParser, IParser
{
    public override List<object> GetRequiredDefinitionSpecifics() => [1];

    public override List<object> GetRequiredTypeSpecifics() => [(byte)1, true];

    public override Type UpdateMessageType => typeof(UpdateConditionMessage);
}
