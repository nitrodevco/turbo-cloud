# Client features: Habbo's newer packet layouts

- [Introduction](#introduction)
- [The hello tail](#the-hello-tail)
- [On the session](#on-the-session)
- [Feature serializers](#feature-serializers)
- [room_dimmer_lights](#room_dimmer_lights)
- [pet_inventory_locations](#pet_inventory_locations)
- [Build 116 without a feature](#build-116-without-a-feature)
- [Not client capabilities](#not-client-capabilities)

## Introduction

From build 116 (protocol `FLASH34`) Habbo's client ends its hello with a list of **features**: the
newer layouts of Habbo's own packets it reads. A server that sees a name writes those packets the
new way for that connection; a client that sends no list reads the layouts it always did. One
revision therefore serves both.

| Feature | Constant | Server |
| --- | --- | --- |
| `avatar_collectibles` | `ClientFeatures.AVATAR_COLLECTIBLES` | kept on the session, no layout yet |
| `paid_name_change` | `ClientFeatures.PAID_NAME_CHANGE` | kept on the session, no layout yet |
| `pet_inventory_locations` | `ClientFeatures.PET_INVENTORY_LOCATIONS` | implemented, [below](#pet_inventory_locations) |
| `private_marketplace_offers` | `ClientFeatures.PRIVATE_MARKETPLACE_OFFERS` | kept on the session, no layout yet |
| `room_dimmer_lights` | `ClientFeatures.ROOM_DIMMER_LIGHTS` | implemented, [below](#room_dimmer_lights) |

The names are in `Turbo.Primitives/Networking/Features/ClientFeatures.cs`.

## The hello tail

`ClientHelloMessage` is the connection's first packet:

| Field | Type | |
| --- | --- | --- |
| production | `string` | selects the revision |
| platform | `string` | `FLASH34` in build 116 |
| client platform | `int` | |
| device category | `int` | an older client's hello ends here |
| count | `int` | build 116 on |
| feature name | `string`, `count` times | |

`ClientHelloMessageParser` reads the tail only when bytes remain after the device category. The
count is bounded by what the packet holds and by `ClientFeatures.MAX_ADVERTISED` (64); a negative
count names nothing, a name longer than `ClientFeatures.MAX_NAME_LENGTH` (64) is dropped, and a
count with fewer names behind it than it says fails like any other short packet.

## On the session

`ClientHelloMessageHandler` hands the names to the session (`MessageContext.SetClientFeatures`),
which keeps only those in `ClientFeatures.KNOWN` and ignores the rest. They are read from
`ISessionContext.ClientFeatures` (a `FrozenSet<string>`, empty until the hello and for an older
client). The set belongs to the connection, like its revision: nothing in a grain holds it.

## Feature serializers

A serializer takes a composer and nothing else, and its bytes are cached and shared by every
session a broadcast reaches (`ComposerPayloadCache`), so a serializer cannot ask who it writes
for. A feature's layout is a second serializer for the same composer type instead, registered in
the revision's `FeatureSerializers` (feature name, then composer type), beside the plain one that
stays in `Serializers`:

```csharp
public IReadOnlyDictionary<
    string,
    IReadOnlyDictionary<Type, ISerializer>
> FeatureSerializers { get; } =
    new Dictionary<string, IReadOnlyDictionary<Type, ISerializer>>
    {
        {
            ClientFeatures.ROOM_DIMMER_LIGHTS,
            new Dictionary<Type, ISerializer>
            {
                {
                    typeof(RoomDimmerPresetsMessageComposer),
                    new RoomDimmerPresetsMessageComposerSerializer(
                        MessageComposer.RoomDimmerPresetsMessageComposer,
                        keepFurnitureLights: true
                    )
                },
            }.ToFrozenDictionary()
        },
    }.ToFrozenDictionary(StringComparer.Ordinal);
```

`PackageEncoder` writes a composer with the feature serializer when the session has the feature
and the revision has one for the composer's type, and with the plain serializer otherwise. The
cache keys bytes by serializer as well as composer, so the two layouts of one broadcast are held
apart. When two features a session has both register the same composer type, the feature whose
name sorts first (ordinal) writes it; none do today, and a packet two features change needs one
serializer that knows both.

To add a layout: write the serializer (the same class with a constructor flag when it differs by
a field, as the moodlight's does), register it under its feature with the header of the plain
one, and leave the plain serializer writing exactly what it wrote. An incoming packet a feature
changes needs nothing here when the older client already sent the field, as below.

## room_dimmer_lights

Each moodlight preset gains "keep furniture lights unchanged" (`DimmerPresetSnapshot.KeepFurnitureLights`).

| Where | Without the feature | With it |
| --- | --- | --- |
| `RoomDimmerPresetsMessageComposer`, each preset | `int id`, `int type`, `string color`, `int brightness` | the same, then `bool keepFurnitureLights` |
| `RoomDimmerSavePresetMessage` | `int presetId`, `int effectType`, `string color`, `int brightness`, `bool apply`, `bool` (always false), `int objectId` | the same bytes; the second `bool` is the flag |
| The furni's data | `state,presetId,effectType,#RRGGBB,brightness` | the same, with `,true` after it while the active preset keeps furni lights |

The save packet and the furni's data are not gated: an older client always sends false, and reads
only the first five parts of the data. The flag is stored with the preset in the item's `dimmer`
extra data section; a preset stored before it existed reads with it off, so nothing is migrated.

## pet_inventory_locations

The pets tab also lists the player's pets standing in rooms, each with its room. Build 116 reads
every pet through one parser (`PetData`), so the feature changes all four packets that carry one:

| Composer | Without the feature | With it |
| --- | --- | --- |
| `PetInventoryEventMessageComposer` | `int totalFragments`, `int fragment`, `int count`, then each pet in the inventory | the same, the pets in rooms among them |
| `PetAddedToInventoryEventMessageComposer` | pet, `bool openInventory` | pet with its location, `bool openInventory` |
| `PetReceivedMessageComposer` | `bool boughtAsGift`, pet | `bool boughtAsGift`, pet with its location |
| `PetRespectNotificationEventMessageComposer` | `int respect`, `int petOwnerId`, pet | the same, pet with its location |

A pet is `int id`, `string name`, `int typeId`, `int paletteId`, `string color`, `int breedId`,
`int partCount`, `partCount` times (`int`, `int`, `int`), `int level`, `int rarityLevel`. Its
location follows the rarity level: `int roomId`, `-1` for a pet in the inventory, and only for a
room `string roomName`, `string roomDescription` (`PetDataSerializer.SerializeWithLocation`). A
pet joining the inventory or just bought is always `-1`; a respected pet names the room it was
respected in (`PetRespectNotificationEventMessageComposer.Room`).

`GetPetInventoryMessageHandler` asks the inventory for the list with locations
(`IInventoryGrain.SendPetInventoryWithLocationsAsync`) only for a session with the feature. The
inventory reads the player's placed pets from their rows and the rooms through
`INavigatorService.GetRoomsByIdsAsync` (a loaded room live, the rest from the room cache), and
puts them in the fragments by id among the others, with their rooms in
`PetInventoryEventMessageComposer.Rooms`. The plain serializer writes only the pets with no room,
so an older client's list stays the pets in the inventory whatever the composer holds.

Placing and picking up are told as before: `PetRemovedFromInventory` when a pet is placed, on
which build 116 keeps the pet, marks it as in the room it is in and asks for the list again, and
`PetAddedToInventory` (ending `-1`) when it comes back. A placed pet's row is written on the
room's next hand-over, so its level in the list can trail the room by a moment; a rename is
written at once.

## Build 116 without a feature

What build 116 added that no feature name gates. The packets are in the revision's block for
newer-client packets:

| Header | Direction | Message | Fields |
| --- | --- | --- | --- |
| 7700 | in | `GetPetRetirementPriceMessage` | none |
| 7700 | out | `PetRetirementPriceMessageComposer` | `int credits` |
| 7701 | in | `RetirePetMessage` | `int petId` |
| 7701 | out | `PetRetirementResultMessageComposer` | `int petId`, `bool success` |
| 7702 | in | `RenamePetMessage` | `int petId`, `string name` |
| 7702 | out | `PetRenameResultMessageComposer` | `int petId`, `bool success` |

- **Retirement** deletes a pet in the inventory for good (`IInventoryGrain.RetirePetAsync`, cause
  `pet.retire`). `Turbo:Inventory:PetRetirementCreditsPrice` (default 0) is charged first and given
  back when the delete fails; a player who cannot pay keeps the pet. The client only shows the
  number, so whether Habbo charges or pays it is not known: Turbo charges. A pet standing in a
  room is refused; the client offers retirement for none.
- **Rename** is the owner's alone. A pet standing in the room the player is in is renamed by the
  room (`IRoomGrain.RenamePetAsync`: the room sees it leave and arrive under the new name, and
  the row is written before the answer); any other is asked of the inventory
  (`IInventoryGrain.RenamePetAsync`). Both hold the name to the rules a new pet's name is held to
  (`PetNames.Validate`), and the room refuses a dead monsterplant waiting to be revived, for
  which the client hides the button. A pet standing in another room is not renamed from afar.
- **Door mode 7, friends only** (`RoomDoorModeType.FriendsOnly`) is saved by the room settings
  like any other door. The owner, rights holders (in a group's homeroom, whoever the group gives
  rights), staff with `room.enter.locked` or control of every room, a teleporter arrival and the
  owner's friends walk in; anyone else is answered as a room that takes no visitors is
  (`CantConnect` with `NoEntry`, then `CloseConnection`), because the client asks nothing before
  it tries the door. The room stays listed in the navigator.

## Not client capabilities

[Client capabilities](client-capabilities.md) are Turbo's own extensions, outside Habbo's
protocol: a client asks for them after login in `TurboClientCapabilitiesMessage` (header 30000),
they are versioned and negotiated, the presence grain keeps what was accepted, and they add
packets Habbo does not have. Client features are Habbo's: named before login in the hello, never
answered, kept on the connection, and they only choose between two layouts of a packet Habbo
already has. A client may have either, both or neither.
