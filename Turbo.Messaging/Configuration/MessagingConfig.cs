using System;
using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline.Core.Configuration;

namespace Turbo.Messaging.Configuration;

public class MessagingConfig : PipelineConfig
{
    public const string SECTION_NAME = "Turbo:Messaging";

    public Func<ISessionContext, string> PartitionKey { get; set; } = m => m.SessionID;
}
