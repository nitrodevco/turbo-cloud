# Packet Handler Rules

- [Packet handler extensions](#packet-handler-extensions)

[`AGENTS.md`](../../AGENTS.md) holds the rules every handler follows (orchestration only, no
database, the three ways to send a composer). This page is for logic that two or more handlers
share.

## Packet handler extensions
When two or more handlers do the same thing around their grain call — the same grain call, the
same mapping from a snapshot to a composer, the same failure-to-composer mapping — that
behaviour goes in one extension class, not in each handler.
- **Shape:** `internal static class <Thing>Extensions` in the folder of the handlers that use it,
  in that folder's namespace. Methods extend `MessageContext` (`ctx.Send…Async`,
  `ctx.…Async`) and take the `IGrainFactory` or service they need as an argument. A class does
  not hold injected state.
- **Naming:** `Send<Thing>Async` fetches or receives a result and sends the composer to the
  sender. `Send<Thing>FailureAsync(result)` sends only the refusal and returns
  `Task<bool>`, which is whether the operation went through. A caller with more to do stops on
  `false`.
- **Guards:** if every caller would write the same guard (`PlayerId`, `RoomId`, object id), it
  goes inside the extension. The handler keeps only checks on its own payload.
- **Existing ones. Reuse them before writing a new one:**

  | Extension | Used for |
  |---|---|
  | `Catalog/CatalogPurchaseExtensions` | `PurchaseOfferAsync`, `SendPurchaseFailureAsync`, `SendBalanceFailureAsync`, `SendPurchaseErrorAsync`. Every catalog refusal goes through these, including LTD raffle errors. |
  | `Navigator/LegacyRoomSearchExtensions` | `SendLegacySearchResultAsync` |
  | `Room/RoomItemInteractionExtensions` | `InteractWithRoomItemAsync`, for any packet that is "do `FurnitureInteraction` X to this item" |
  | `RoomSettings/RoomSettingsSaveExtensions` | `ResolvePlayerFlatCategory` and `SendRoomSettingsSaveFailureAsync` |
  | `RoomSettings/RoomFilterExtensions` | `SendRoomFilterAsync`, which sends the room's whole word filter |
  | `Inventory/Badges/BadgeRequestExtensions` | `SendBadgeRequestFulfilledAsync` |
  | `Users/GuildMemberMgmtResultExtensions` | `SendGuildMemberMgmtFailureAsync` |
  | `Userdefinedroomevents/Wiredmenu/WiredVariableHoldersExtensions` | `SendWiredVariableHoldersAsync`, which lists who holds a wired variable |
  | `Userdefinedroomevents/Wiredmenu/WiredVariablesForObjectExtensions` | `SendWiredVariablesForObjectAsync`, which lists the variables one target holds |
  | `Help/CallForHelpExtensions` | `SubmitCallForHelpAsync` (a report from the help window, the messenger or a photo, answered with `CallForHelpResult`) and `SendMyCfhReportStatusAsync`, which lists the player's own calls for help |

- **Where it does not go:**
  - A one-line grain call that encodes a rule belongs in `GrainFactoryExtensions` in
    `Turbo.Primitives`, for example `SendComposerToPlayerAsync` and `HasActiveClubAsync`.
  - A parser or other pure helper belongs beside its type, for example `WallPosition.TryParse`.
  - Do not extract a single `ctx.SendComposerAsync(new X { … })`. The extension has to own a
    decision or a mapping.
- An extension is not a place for side effects on other grains. The rule that a handler does
  guard, call, report still applies. See [Grain Rules](grains.md#grains-are-not-reentrant-side-effects-on-other-grains-that-call-back-go-in-the-service).
