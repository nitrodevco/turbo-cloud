# Reception responses

The reception timing handlers resolve the client's date/code schedules against UTC
and echo the original schedule or timestamp so the client can match the response.

## Bonus rare

The bonus rare widget shows how many credits a player still needs before a furniture is theirs.
Campaigns are kept in `bonus_rare_campaigns` and edited on the admin panel's **Hotel view >
Bonus rare** tab; the last one started and not ended runs, and with none the widget is hidden.

| Field | What it is |
| --- | --- |
| Code | What progress is kept under (`player_bonus_rare_progress.campaign_id`). A new code starts everyone afresh. |
| Furniture | The definition given, by name. Its sprite id is sent to the client. |
| Product code | The product data entry the widget names the reward by; the furniture's name when empty. |
| Credits | Credits for each reward. |
| What counts | **Credits bought** or **credits spent in the catalogue**. |

- **Credits bought** count only when recorded with the purchase's own reference
  (`IBonusRareService.RecordPurchaseAsync`): by the hotel's shop, or by staff on the panel
  (**Record bought credits**, `POST /api/hotel-view/bonus-rare/purchases`). Each reference is kept
  in `bonus_rare_receipts` and counts once. Wallet balances and client requests are never taken as
  evidence of a purchase.
- **Credits spent in the catalogue** are what the server debited for an offer in the normal
  catalogue (not gifts, not the Builders Club).
- Each time a player's progress reaches the target, the target is taken off in the database
  (`credits = credits - target where credits >= target`) before the furniture is put in their
  inventory, so a target reached gives one reward whichever silo counts it. A furniture that
  can't be given leaves the credits where they were, to be given with the next credits counted.
  The widget is told where the player stands after every count.
- `AddBonusRareCampaigns` turned the old `Turbo:Catalog:BonusRare` defaults into a campaign
  (`bonusbag26_3`, 120 credits, credits bought), so progress already kept carries on.
