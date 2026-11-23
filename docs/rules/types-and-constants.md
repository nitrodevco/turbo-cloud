# Types & Constants

- [Type placement](#type-placement)
- [Constants and magic values](#constants-and-magic-values)

Where a new type, snapshot, enum or constant goes, and how a database row becomes a snapshot.
[`AGENTS.md`](../../AGENTS.md) keeps the short form and sends a change here when it adds a type,
a snapshot mapping or a literal.

## Type placement
- Every snapshot type lives in its own file under `Turbo.Primitives`, named after the type, in the
  folder for its domain (`Turbo.Primitives/<Domain>/Snapshots/<Name>Snapshot.cs`, for example
  `Turbo.Primitives/Rooms/Snapshots/RoomEventSnapshot.cs`).
- Never declare a snapshot inside another type's file (a composer, message, grain or service).
- The same applies to shared enums and DTOs: one public type per file, placed under the domain it
  belongs to, so other modules can use it without depending on the file that happened to need it
  first.
- A record stored as a JSON section of a furniture item extra data (`PetPackageData`,
  `PresentStorage`, `WiredRewardClaim`, ...) lives in `Turbo.Primitives/Furniture/ExtraData/`,
  namespace `Turbo.Primitives.Furniture.ExtraData`, next to the `FurnitureExtraDataSections`
  reader. It names its section in a `SECTION` constant when it owns one. Sections are read
  through `FurnitureExtraDataSections.Read`, which takes the logger so an unreadable section is
  logged with its name, and written through `FurnitureExtraDataSections.ToNode` (camelCase, as
  the room's extra data writes them); do not deserialize or build a section by hand or keep its
  name in the logic class. The dimmer read its presets by hand under a private name, and the
  Morningstar import wrote five sections as hand-built JSON objects. Never put a section record directly under `Turbo.Primitives/Furniture/` or beside the
  logic that reads it. Client-facing key and
  state tables (`PresentData`, `MannequinData`, `DiceStates`) are not section records and stay
  under `Turbo.Primitives/Furniture/`.
- A database row becomes a snapshot through an extension method on the entity, in
  `Turbo.Database/Extensions/<Domain>EntityExtensions.cs` (`entity.ToSnapshot(...)`,
  `RoomEntity.ToInfoSnapshot(...)`). Whatever the row does not hold (owner name, grouped child
  ids, a resolved definition, "now") is a parameter, so the mapping stays a pure function with
  no provider or grain inside it. Do not write `new XSnapshot { Id = entity.Id, ... }` in a
  grain, provider or service: the second place that needs the same mapping will copy it and the
  two will drift. Two things stay inline: a column-trimmed projection inside a query
  (`.Select(x => new PlayerBadgeSnapshot { ... })`, so SQL fetches only those columns) and a
  snapshot built from grain state rather than from a row.
  When one snapshot record derives from another (`RoomSnapshot : RoomInfoSnapshot`), map the
  shared fields once: the derived record takes the base in a `[SetsRequiredMembers]` constructor
  that calls the record copy constructor (`: base(info)`), and its extension is
  `new(entity.ToInfoSnapshot(...)) { room-only fields }`. That attribute switches off the
  compiler's required-member check for that constructor, so keep it to the one mapping site and
  keep a parameterless constructor beside it for everything else.
  The same applies to a factory that builds a derived snapshot from an instance of its base, not
  just to an entity mapping. **Most derived snapshots in the tree do not do this yet** — of the
  derived records under `Turbo.Primitives`, only `RoomSnapshot`, `ClubExtendOfferSnapshot` and
  `GuildSnapshot` take their base in a constructor; the rest either add no fields (nothing to
  do) or re-list the base's fields by hand, and those are the ones to convert as you touch them.
  This is not a tidiness rule. `RoomActiveSnapshot.From` copies nineteen base fields one by one
  and silently stopped copying `HiddenByBc` when that field moved up to `RoomInfoSnapshot`; the
  field is not `required`, so nothing failed to compile, and a room hidden by Builders Club goes
  on being listed by the navigator for as long as it is active, because `NavigatorService`
  filters live rooms on exactly that field. A `: base(room)` constructor could not have lost it.
- What a furni leads to is stored on the item, in the `RoomLinkerData` extra data section: a
  fixed `RoomId` for a room linker, the paired `ItemId` for a teleporter. There is no link table.
  A pair's room is never stored, because either half can be picked up and placed elsewhere;
  resolve it when needed (`RoomFurniModule.GetRoomIdOfItemAsync`). A dangling `ItemId` (the pair
  was deleted) simply leads nowhere.
- A hierarchy the code switches over by kind is `closed` (C# 15) when all its direct subtypes are
  in its own assembly, as `WiredBox` is: a switch expression over it then needs no `_ =>` arm, and
  a new kind stops compiling wherever it is not handled. Before, `WiredBox` faked this with a
  `private protected` constructor, and the switch that builds a box's room logic ended in a
  `throw` for a kind that could not exist. Only switch expressions are checked: a switch
  statement keeps its `default`. A type plugins derive from directly (`WiredEffect`) stays open.
- A value that is exactly one of a few types is a `union` (C# 15), not a tuple of nullables read
  back with `!` or an `object` slot with a dummy `_ =>` arm: `CatalogEndpoints.LogsForm` (the
  packet logs, or why the form had none) and `GamedataFurnitureService.ImportedRow`. A union stays
  private to the class that uses it, and never crosses a grain call (`docs/rules/grains.md`).
- A collection built with a comparer or a capacity is `[with(StringComparer.Ordinal), ...]` where
  it has a target type (a field, a return, an assignment). A copy of another collection stays
  `new HashSet<int>(source)` or `[.. source]`: `[with(source)]` compiles to the same constructor
  call, but reads as an option rather than as the contents.

## Constants and magic values
- Do not scatter hardcoded literals or `const` fields through implementation files. Give them a home:
  - a value the protocol or domain defines (message types, result codes, entry kinds) becomes an
    **enum** in `Turbo.Primitives/<Domain>/Enums/`, and the serializer casts it. The composer or
    snapshot field has the enum's type; an `int` field with `(int)SomeType.Member` at the call
    site is the same cast in the wrong place (`RoomSettingsErrorEventMessageComposer.ErrorCode`,
    `VariableFxConfigSnapshot.ShowMode` / `Category`, `PurchaseErrorMessageComposer.ErrorCode`,
    which sat one `switch` away from `PurchaseNotAllowedMessageComposer.ErrorType` carrying the
    same enum properly). Zero counts: when the client reads `0` as
    an answer ("can rent"), the enum gets that member (`RentableSpaceRentFailedType.None`)
    instead of a `const Type X = 0` beside the code that sends it;
  - a set of related identifiers the client sends (search codes, cache keys) becomes one **shared
    static class** in `Turbo.Primitives/<Domain>/` (see `NavigatorSearchCodes`,
    `NavigatorListingKeys`);
  - anything an operator may want to tune (limits, lengths, timeouts, intervals, caps) becomes a
    **config option** on the module's config class, bound from `appsettings.json`.
- A unit conversion is not a constant to declare: `MS_PER_SECOND = 1000` appeared in two furni
  classes in one change. Convert through `TimeSpan` (`TimeSpan.FromSeconds(x).TotalMilliseconds`),
  or through the domain's own helper where one exists (`WiredPulses`).
- Keep an inline literal only where it is local and self-evident, and comment why.
