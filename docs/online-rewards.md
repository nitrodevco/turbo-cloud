# Online rewards

Currency paid to players for time online: every so many minutes of a player's session, each
player logged in gets an amount of a currency. It is operator tooling, like `:give @online` on a
timer, not a Habbo feature, so it is off until a hotel turns it on (`Turbo:OnlineRewards:Enabled`)
and gives it rules. While it is off nothing runs but one look at the setting every
`CheckSeconds`.

## Settings

Every setting but `CheckSeconds` applies at once, from the panel's Online rewards page or its
Settings page (`Turbo:OnlineRewards`).

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `false` | Pays anyone at all. The rules are kept while it is off. |
| `Rules` | `[]` | One rule per currency: `Currency` (as `:give` names it), `Amount`, `IntervalMinutes`, `HcBonus` (added for a Habbo Club member), `DailyCap` (the most per player per UTC day; 0 for none), `InRoomOnly` (paid only to a player in a room when the payout comes round, not on the hotel view), `IdleMinutes` and `SkipSleeping` (the rule's own; left out, the hotel's below), `Enabled`. A second rule for a currency, a rule without an amount or interval, and a currency the hotel lacks pay nothing (the last is logged once). |
| `IdleMinutes` | `10` | A player who has done nothing for this long (the session's `LastActiveUtc`: the client's own timers don't count) misses the payout. 0 leaves only `SkipSleeping`. For every rule without its own. |
| `SkipSleeping` | `true` | A player whose avatar sleeps in its room (the room's idle sleep, or the sleep they chose) misses the payout. Read with `GetAvatarPositionAsync`, which never wakes the avatar. For every rule without its own. |
| `MaxConcurrentPayouts` | `8` | How many players are paid side by side when payouts fall due together. |
| `CheckSeconds` | `10` | How often the server looks for payouts due; a payout is at most this late. Read at start. |

A payout is capped by `Turbo:Operations:MaxCurrencyAmount`, as `:give` is.

## When a player is paid

Each login has its own clock, started when the connection opened: a rule of 15 minutes pays at
15, 30, 45 minutes online. A rule turned on, or changed, while a player is online first pays at
their next whole interval. Each rule is asked for itself, so a player on the hotel view or asleep
can miss one rule's payout and get another's. A payout missed (the player idle, asleep, not in a
room for a rule that needs it, the feature off, the server busy) is
never made up, and nothing is paid for time offline or before a restart. Logging in again starts a
new clock; the daily cap holds across logins and restarts (`player_online_rewards`, one row per
player and currency, kept only for capped rules).

## What the player sees

The payout goes through the wallet like `:give` (`IPlayerWalletGrain.CreditAsync`), which sends
the packet the client redraws that balance from and nothing else: `HabboActivityPointNotification`
(amount and change) for activity points, `CreditBalance` for credits. Flash shows its own
notification for diamonds (type 5, `notifications.text.loyalty.received`) and rings the purse for
duckets and credits (`HabboCatalog.onActivityPointNotification`, `onCreditBalance`). Each payout is
a `reward.online` movement (actor: the player, cause ref: the currency), so it is in the panel's
currency log while movements are recorded.

## How it runs

One `OnlineRewardService` per server pays the players connected to that server, so a player is
paid by one server only. Each `CheckSeconds` it reads the session gateway, keeps a small clock per
login (dropped with it) and pays those due, at most `MaxConcurrentPayouts` at a time. Nothing runs
per player between payouts: no grain timer or reminder per player to register, persist or leak.

Measured with 1,000 players online and two rules (`OnlineRewardServiceTests` set-up, a fake
wallet): the clocks hold about 390 KB; a check where nobody is due takes 0.6 ms and allocates
2 KB; a check where all 1,000 are due spends 25 ms on its own work, the wallet writes on top.
