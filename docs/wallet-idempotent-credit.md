# Idempotent wallet credit

`IPlayerWalletGrain.CreditAsync(kind, amount, reference, ct)` credits a balance at most once
per `(player, reference)`. Use it when a credit must survive a retry, for example a plugin that
commits its own database transaction first and then credits the wallet (outbox pattern): if it
crashes before recording success, it repeats the call and the player is not paid twice.

- `reference` is opaque. By convention it is `<plugin-key>:<id>`, for example `shop:order-1042`.
  It must be non-blank and at most `WalletCreditReference.MaxLength` (128) characters, otherwise
  the call throws. MySQL compares it case-insensitively, so do not rely on case to tell two apart.
- The scope is the player, not the currency: one reference credits one currency once.
- Results: `Applied`, `AlreadyApplied` (nothing changed; treat as success), `Rejected`
  (non-positive amount, unknown currency, or the balance would overflow; no receipt is kept).
- The balance and the receipt (`wallet_credit_receipts`) are saved in one `SaveChanges`.

```csharp
var wallet = grains.GetPlayerWalletGrain(playerId);
var result = await wallet.CreditAsync(CurrencyKind.Credits, 50, $"shop:order-{order.Id}", ct);
if (result != WalletCreditResult.Rejected)
    await MarkOrderCreditedAsync(order, ct); // safe to repeat after a crash
```

The existing `CreditAsync(kind, amount, ct)` is unchanged and keeps no receipt.
