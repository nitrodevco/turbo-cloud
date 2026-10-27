# Contribution Guide

- [Introduction](#introduction)
- [Bug Reports](#bug-reports)
- [Which Branch?](#which-branch)
- [Coding Style](#coding-style)
- [Tests](#tests)
- [AI-Assisted Contributions](#ai-assisted-contributions)
- [Documentation](#documentation)
    - [Writing a Page](#writing-a-page)
    - [The Sidebar](#the-sidebar)
    - [Building the Site](#building-the-site)

## Introduction

Turbo is built in the open, and every pull request is welcome: a fix, a feature, a test that
catches a bug, a page of documentation. This guide says where a change goes and what it needs
before it is merged. The full checklist for core code lives in
[`CONTRIBUTING.md`](../CONTRIBUTING.md) and [`AGENTS.md`](../AGENTS.md); this page is the short
tour.

## Bug Reports

The best bug report is a pull request with a failing test. If you can't fix the problem yourself,
open an issue with what you did, what you expected and what happened instead, including the
server log lines around it. A bug in how the hotel behaves compared with Habbo is easiest to act on
with the Habbo behaviour described step by step.

## Which Branch?

| Repository | Branch | What it holds |
| --- | --- | --- |
| [`nitrodevco/turbo-cloud`](https://github.com/nitrodevco/turbo-cloud) | `main` | The server, the plugin framework, the plugin template and this documentation |
| [`nitrodevco/nitro-next`](https://github.com/nitrodevco/nitro-next) | `pixi-render` | The client, its client-module SDK and the client's guides (the [Client & Themes](https://github.com/nitrodevco/nitro-next/blob/pixi-render/docs/template-patches.md) pages) |

Open each pull request against that branch, rebased on it, with one change per pull request.
The pull request template asks why, what, the evidence (tests, figures) and what is left out, and
whether the change affects **plugin authors**. One that does adds its entry to the
[plugin changelog](plugin-changelog.md) in the same pull request, and a breaking one says what to do
in the [upgrade guide](upgrading.md).

## Coding Style

The server follows the .NET conventions the repository's analyzers enforce, formatted by
CSharpier. Run bootstrap once, and the git hooks check your work for you:

```shell
sh scripts/bootstrap.sh            # or: pwsh -File scripts/bootstrap.ps1
```

- `pre-commit` runs the fast check: `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck`.
- `pre-push` runs the full gate: `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate`.

If the gate fails on formatting, `dotnet tool restore` then `dotnet csharpier .` fixes it. The
architecture rules (handlers that only orchestrate, no database calls from a handler, the three
ways to send a composer) are in [`AGENTS.md`](../AGENTS.md) and [`CONTEXT.md`](../CONTEXT.md),
with a sample of each shape in [Code Patterns](patterns/README.md).

## Tests

A change to behaviour comes with a regression test in `Turbo.Tests` that fails without the change
and passes with it. Drive the entry point production uses (a grain method, a room module, or the
client's bytes through `PacketHarness`) and assert on what a player or the client would see.
`Turbo.Tests/Support` builds rooms, grains and packet round-trips without a silo. The
[Testing](testing.md) page covers plugin tests.

```shell
dotnet test Turbo.Tests/Turbo.Tests.csproj --filter MyFeatureTests
```

## AI-Assisted Contributions

AI tools are welcome. Give them `AGENTS.md`, `CONTEXT.md` and the closest sample in
`docs/patterns/`, and review what they write as you would anyone's. Say in the pull request that
you used one, and be ready to explain complex generated code in your own words. See
[Agentic Development](ai.md).

## Documentation

Documentation changes in the **same pull request** as the code it describes. A new hook, setting,
command or packet is not done until its page says how to use it.

### Writing a Page

Every page has the same shape, so a reader always knows where to look:

1. a `# Title`;
2. a linked table of contents of its `##` and `###` sections;
3. an `## Introduction`: what the feature is and why you would use it, in a paragraph or two;
4. task-shaped sections ("Defining a Command", "Sending a Message") with code you can copy.

Write plainly and talk to the reader ("you"). Name real types and members, and check each one
against the code: a page that names an API that does not exist is a bug. Use callouts for what a
reader must not miss:

```markdown
> [!NOTE]
> Something useful to know.

> [!WARNING]
> Something that will hurt if ignored.
```

Link to another page with a relative link (`[settings](settings.md)`), and to code with a relative
path (`[PackageHandler](../Turbo.Networking/Package/PackageHandler.cs)`): both work on GitHub, and
the site turns links to code into GitHub links.

### The Sidebar

[`docs/documentation.md`](documentation.md) is the sidebar: a `- ## Section` line for each section
and an indented `- [Title](page.md)` line for each page, in order. A page that is not listed there
is still built and searchable, but nobody will find it by browsing; add yours where it belongs.

### Building the Site

The site is [VitePress](https://vitepress.dev), configured in `docs-site/`. It reads `docs/` as it
is, so you never edit the site to change a page. With Node 22:

```shell
cd docs-site
npm ci
npm run dev        # http://localhost:5173, reloads as you edit docs/
npm run build      # what CI runs: fails on a dead link or a link to a heading that is gone
```

The Client & Themes pages are nitro-next's own `docs/`, copied into `docs/client/` (ignored by
git) as the site builds. To build against a client checkout you already have, set
`NITRO_NEXT_DOCS` to its `docs` folder first. Change those pages in nitro-next.
