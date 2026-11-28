using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Pets;

/// <summary>
/// Sent once when the client's pets tab initialises (build 116 on); it offers retirement only
/// after the answer.
/// </summary>
public record GetPetRetirementPriceMessage : IMessageEvent { }
