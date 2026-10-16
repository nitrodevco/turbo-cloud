using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>
/// "Generate" or "Regenerate" on one of the Web API add-on's two keys (Flash
/// <c>WiredGenerateWebApiKeyMessageComposer(wiredId, isReadKey)</c>, sent by
/// <c>VariablesWebApiAddon.onClickGeneratedReadKey</c> / <c>onClickGeneratedWriteKey</c>).
/// </summary>
public record WiredGenerateWebApiKeyMessage : IMessageEvent
{
    /// <summary>The add-on box being edited.</summary>
    public required int WiredId { get; init; }

    /// <summary>True for the key for reading, false for the key for writing.</summary>
    public required bool IsReadKey { get; init; }
}
