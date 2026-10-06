# Reception responses

The reception timing handlers resolve the client's date/code schedules against UTC
and echo the original schedule or timestamp so the client can match the response.

## Bonus rare

Configure `Turbo:Catalog:BonusRare` in server configuration:

```json
{
  "Enabled": true,
  "CampaignId": "bonusbag26_3",
  "FurnitureName": "bonusbag26_3",
  "ProductCode": "bonusbag26_3",
  "CreditsRequired": 120
}
```

These are the defaults. `FurnitureName` resolves the server furniture definition;
its sprite ID is sent to the client. `ProductCode` identifies the client product
data entry. A missing definition or disabled campaign hides the promotion.
The client controls the promotional artwork through its own configuration.

The migrations are applied when the server starts, or ahead of time with `Turbo.Main migrate` ([database.md](database.md)).
`player_bonus_rare_progress` stores progress per player and campaign. A missing
row means zero purchased credits. Use a new campaign ID when starting a new offer
to avoid reusing progress from an earlier campaign.

The response is informational. Payment processing and reward delivery are not
implemented by this change. A trusted purchase integration must record paid
credits and deliver rewards with transaction and duplicate-payment protection
before this can operate as a live purchase promotion. Wallet balances and
client requests must never be treated as purchase evidence.
