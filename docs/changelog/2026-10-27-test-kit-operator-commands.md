---
title: "Test kit: operator commands run, and each plugin compiles once a test run"
date: 2026-10-27
pr: turbo-cloud#476
breaks: no
who: "plugins with operator commands and tests; every plugin test suite (faster)."
---

**What**: `SaysAsync` runs an operator command (`IOperatorCommand`) through the hotel's own
operator command runner, as a room hands it over on a server: nodes, arguments, the plugins'
vetoes, `:confirm` for a command reaching `Turbo:Commands:ConfirmAtPlayers` players (settable
with `hotel.Settings.Set`), whispered replies, notices as alerts (`hotel.Notify.Alerts`) and the
command log (`hotel.CommandLog.Entries`). The test hotels of a run share one compile of the same
sources: a test after the first loads its plugin in tens of milliseconds instead of compiling it
again (a 28-test plugin suite: 28 s to 9 s), and a hotel no longer waits up to 5 s at its end
for the plugin's code to leave memory. Core's nodes granted by default (`command.confirm`, ...)
are held by every test player, as on a hotel.

**Breaks plugins**: A test that called its operator command's class to get around the kit
keeps working.

**Action**: none; a plugin's own operator command plumbing in its tests can go
(`docs/plugin-testing.md`, "Commands").
