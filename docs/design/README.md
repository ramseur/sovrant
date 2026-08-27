# Design

This folder is the design record for Sovrant's user-facing surfaces. It exists to let anyone — human or agent — **verify a design before and after it ships**, across every frontend, without having to run the app.

## Scope

- **Web** (`web/`) — Blazor Server app, port 5100.
- **Desktop** (`desktop/`) — Avalonia app.
- **CLI** (`cli/`) — not started yet. Add a folder here once CLI gets a real design pass; today it's explicitly out of scope per the README ("functional but actively being refined").

Each platform folder holds **versioned, static HTML previews** — self-contained `.html` files that render the design with real interaction (nav switching, hover, light/dark, etc.) but no backend. They are not the app; they're a fast, shareable way to look at a screen's design in a browser and compare it against what's actually implemented in `src/Sovrant.Web` / `src/Sovrant.Desktop`.

## Versioning

Each platform folder is a flat list of versions: `v1.html`, `v2.html`, ... A new version is added whenever a screen or the shared shell (nav, top bar, etc.) gets a deliberate design pass — not for every implementation tweak. `v1` is never edited after `v2` exists; it stays as the historical record of what that revision looked like. If a review needs a specific screen instead of the whole shell, name it `v{n}-{screen}.html` (e.g. `v2-login.html`).

Before opening a new version, skim the changelog note at the top of the current one so you're not redoing a decision that was already made and reverted.

## Current state

**v1** (both platforms) — the left-nav redesign shipped in commit `7970ca3`: collapsible icon+label rail, real line icons replacing the old emoji glyphs, a left accent-bar for the active item, and Admin's 9 destinations grouped under Overview / Access / Safety / System. This is the baseline the rest of the app's design should extend, not fight — new screens should read as the same product as this nav, not a different one bolted on.

## What's next

A formal design pass across every remaining screen, starting with **Login**, then working through each destination behind the nav (Dashboard, Chat, Knowledge's six pages, Agents, Projects, Admin's nine pages, Settings). The nav redesign (v1) is the foundation to build on — keep its type scale, spacing rhythm, icon style, and color tokens rather than introducing new ones per screen.

## How to use this folder

1. Before touching a screen's real implementation, either open the latest version here to confirm what's already agreed, or add a new version if the design is changing.
2. Preview files are just HTML — open directly in a browser, no build step.
3. Once a design is implemented for real, leave the preview file as-is (it's a historical snapshot) and note in the next version's header what changed and why, if anything drifted from the preview during implementation.
