---
title: "Expose MCP resources and prompts and host browser authorization routes"
date: 2026-10-28
pr: turbo-cloud#483
breaks: no
who: "plugin authors exposing MCP resources, prompts or browser authorization flows."
---

**What**: plugins declare MCP resources, URI templates and prompts alongside tools, with
permission checks and test-kit helpers. Web routes can serve HTML, form responses, redirects
and well-known documents. Origin checks follow the selected route and preflight method.
The hotel API also exposes staff room kicks for approval-gated tools.

**Action**: implement the resource and prompt contracts and use their testing helpers.
Use public cross-origin routes only for bearer-token clients; hotel-cookie consent routes
keep their origin protection. Existing plugin routes continue to work.
