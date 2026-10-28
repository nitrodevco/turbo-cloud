---
title: "Stream AI answers and manage providers in the panel"
date: 2026-10-27
pr: turbo-cloud#482
breaks: no
who: "plugin authors using AI and hotel operators configuring providers."
---

**What**: `IAi.StreamAsync` reports text deltas, tool progress and a final response with the same
budgets and stop conditions as prompts. Approval decisions queue continuation in a bounded
background queue. Per-player and per-plugin run limits are shared through Orleans. The panel
offers provider connection tests, write-only key updates and usage by UTC day.

**Action**: handle `AiStreamEnd` for the final stop reason. Deltas precede post-answer middleware;
buffer and moderate them or use `PromptAsync`. Configure providers and allowed plugins under
`Turbo:Ai`; `ResumesAtOnce` and `ResumeQueueCapacity` apply after restart. See `docs/ai-sdk.md`.
