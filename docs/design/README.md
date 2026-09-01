# Design

This folder is the design record for Sovrant's user-facing surfaces. It exists to let anyone — human or agent — **verify a design before and after it ships**, without running the app.

**Scope note:** most entries below describe changes actually applied to production code (`src/Sovrant.Web`, `src/Sovrant.Desktop`), committed and pushed to `development` — not just edits to `web.html`/`desktop.html`. The two mock files stay the source of truth for what a screen *should* look like; the dated sections are the log of real code being brought in line with them. See "Production code touched by this work" below for the full commit-by-commit list.

## Two files. That's the whole thing.

- **`web.html`** — every Web screen.
- **`desktop.html`** — every Desktop screen.

Open either in a browser; no build step. Pick a screen from the index on the left. Each entry is tagged with the pattern that renders it.

A third file will join them when the CLI gets a real design pass. Today it's out of scope — the README calls it "functional but actively being refined".

## 21 screens, 5 patterns

The destinations behind the nav are not 21 designs. They're five patterns plus data:

| Pattern | Screens | What it is |
|---|---|---|
| **Browse** | 13 | Searchable list beside a detail pane. Artifacts, Code Templates, Documents, Memory, Skills, Tools, Agents Library, Projects, Users, Workspaces, Providers, Platform Integrations, System Integrations. |
| **Overview** | 2 | Stat tiles over an activity table. Dashboard (scoped to you), Command Center (scoped to everyone). |
| **Settings** | 4 | Sectioned cards of labelled rows, each row one control plus the sentence explaining it. Settings, Governance, Trust Boundary, Diagnostics. |
| **Conversation** | 1 | Chat. Genuinely its own shape — welcome state, thread, collapsed work strips, composer. |
| **Entry** | 1 | Login. The only screen with no rail. |

This is the point of the folder. Those pages were each built standalone — they share no layout classes today, which is exactly why they drift. Designing the pattern once and treating each screen as pattern + data is what stops it.

**When adding a screen, use an existing pattern.** A new pattern needs a reason the existing four can't express it.

## Parity

**Web and Desktop should match as closely as each platform allows.** `desktop.html` is generated from `web.html` with only the platform chrome swapped — browser tab strip and address bar become a native titlebar and window controls. A diff of the two files should show *only* those chrome lines, the title/heading, and the theme storage key. Anything else that differs is drift.

Current state: 47 changed lines, all accounted for by that list.

## Mark placement (decided 2026-08-28)

The "S"-in-a-square mark showed up everywhere — rail brand row on every screen, 54px on Login — which was wrong. It's brand chrome, not navigation, and repeating it added nothing a user needed. **The mark now appears in exactly two places, both platform chrome, neither one app content:**

- **Web** — the browser tab favicon (`.fav` in the tabstrip mock).
- **Desktop** — the native titlebar/window icon (`.tico` in the titlebar mock; real code already sets this via `Window.Icon`).

Everywhere else — the rail (both platforms) and the Login header — now carries no brand element at all, not even the "Sovrant" wordmark. It's redundant: the tab/titlebar already names the app, and the rail's first visible thing is now the first nav destination, Dashboard. The brand row itself is gone too, not just its contents — the collapse toggle floats on the rail's own border edge instead of owning a header row, so there's no blank space above Dashboard. Shipped in both `web.html`/`desktop.html` and in real code: `MainLayout.razor` + `sovrant.css` on Web, `MainWindow.axaml` on Desktop (Login already had no mark on either platform).

**Resolved (2026-09-01):** the "S" letterform is gone. Explored four abstract concepts (Flow/Orbit/Weave/Monogram) via a side-by-side comparison artifact shown at actual favicon/titlebar sizes — landed on reusing the app's existing lightning-bolt-in-purple-square (`Sovrant.Desktop/Assets/icon.png`, orange `#FF9800` on brand purple `#6D52C6`) rather than commissioning new artwork. That asset already existed as the Desktop window icon; it's now also `Sovrant.Web/wwwroot/favicon.ico`.

One format correction along the way: the first pass shipped an SVG favicon, which modern Chrome/Firefox/Edge support but isn't the actual standard — `.ico` is what browsers request by default and what every browser supports. Regenerated as a proper multi-resolution `.ico` (16/32/48/64/128/256px, via Pillow) from the same source PNG. `web.html`/`desktop.html`'s `.fav`/`.tico` swatches now show the bolt shape (a standard "zap" icon path, filled orange) instead of the placeholder letter.

## Chat: mark reuse + emoji removed (2026-08-31)

A screen-by-screen review against the mark-placement rule above found it was already being violated on the highest-traffic screen: Chat's empty-state hero and every assistant message avatar reused the brand-mark treatment (a colored square with a bold glyph — ⚡ in real code, "S" in the mock), and the six welcome-state suggestion tiles used raw emoji (🤖🤝🌀🎯🎼🔗) despite the nav redesign's "real icons, not emoji" standard.

Fixed on both platforms, matching the pattern already used for the rest of the app: the hero icon and every assistant avatar now render the same chat-bubble line icon used in the rail's own Chat nav item, in neutral (not brand-colored) styling — no square, no letter, no color fill. The six suggestion tiles use the same reused-icon-set approach as the rail (agents/projects/box/refresh/chevron/paperclip icons, matching web `I.*` icon keys and Desktop's `NavIcons.axaml` `StreamGeometry` resources).

`web.html`/`desktop.html` also gained a **Thread / Welcome** toggle on the Chat screen (visible only when Chat is selected) so the mock can actually demonstrate both states the Conversation pattern claims to have — previously `.welcome`/`.wm` CSS existed but was never rendered by `chatHTML()`.

Shipped in: `Chat.razor` + `ChatMessage.razor` + `sovrant.css` on Web; `ChatView.axaml` + `NavIcons.axaml` (4 new `StreamGeometry` keys: `IconSwarm`, `IconMission`, `IconOrchestrate`, `IconConnect`) on Desktop.

**Known remaining emoji** (out of scope for this pass — tracked for the next screen in the rotation): `Chat.razor`'s privacy lock icons (🔒/🔓) and error-banner warning triangle (⚠), plus raw emoji still present in `TopContextBar`, `Artifacts`, `Agents`, `Memory`, `DocumentArtifactCard`, `Orchestration`, `Setup` (Web) and their Desktop equivalents.

## Overview: emoji removed (2026-08-31)

Dashboard and Command Center (`UserDashboard.razor`/`CommandCenter.razor` on Web, `UserDashboardView.axaml`/`CommandCenterView.axaml` on Desktop) both used a `KindIcon()` helper returning one of five emoji (🎯👥🤖💬🔗) prefixed onto every kind pill, plus 🔒/🔓/&#x1F512; for privacy state and ⚠ on the error banner. `web.html`/`desktop.html` never had this problem — the mock's kind pill was always plain text, no icon — so real code was the one out of step here, not the mock.

Fixed to match the mock: `KindIcon()` deleted entirely (both Razor methods, both Desktop `KindIcon` properties/converters-in-XAML) — kind pills now show plain text only. Privacy lock/unlock and the warning-triangle are real line icons now, not emoji: Web inlines SVG directly (browser-rendered, no conversion risk); Desktop gained `IconLock`/`IconUnlock` `StreamGeometry` resources in `NavIcons.axaml`, verified by pixel-sampling the rendered window (padlock shape confirmed correct) and by exercising the equivalent Web toggle (same path logic) since the Desktop click didn't land precisely enough to re-verify interactively.

**Known remaining emoji** (Agents/Browse pattern, next in rotation): the shared `BoolToLockIconConverter`/`BoolToPrivacyLabelConverter` (Desktop) still return 🔒/🔓 text for `AgentsView.axaml` and one more `ChatView.axaml` site (the session-level privacy toggle, distinct from the message-avatar fix already shipped) — left alone this pass since changing the shared converter would require updating all three call sites together to avoid breaking the two not yet in scope.

## Browse: emoji removed (2026-08-31)

Swept the rest of the Browse pattern (Artifacts, Documents, Memory, Agents, Projects, Users/Admin, Workspaces, System Integrations, Platform Integrations — Skills/Tools/Code Templates/Providers were already clean). Same finding as the other two passes: `web.html` never had any of these emoji, so this was real code drifting from the design record. Fixed: warning-triangle error banners (10 files), folder icon (Artifacts), chat-bubble icon (Documents' "Chat to create", Agents' "Launch chat"), lock/unlock (Memory notes, Agents' own-run privacy toggle), a generic package icon replacing the PostgreSQL/Supabase mascot emoji (System Integrations), and a refresh icon replacing the OAuth "waiting" spinner (Platform Integrations). Every icon reused path data already proven earlier this session — no new hand-drawn geometry this pass.

**Left alone, deliberately:** the ✕ close/remove glyph used across ~6 sites (Integrations, WorkspacesAdmin, AdminView). That's a plain typographic symbol (same category as the → ▲▼ sort/link glyphs already in the codebase), not a pictorial emoji — outside what the "real icons, not emoji" standard is targeting.

**Verification gap:** Web was fully verified live (Artifacts, Documents, Memory, Agents, System Integrations) in Chrome, including exercising interactive states. Desktop's build is clean and every icon geometry is one already pixel/screenshot-verified earlier this session, but live interactive verification of this pass's Desktop screens (Documents, Memory, System Integrations, Integrations) wasn't completed — `SetForegroundWindow` silently failed to focus the app window from the automation context, and synthetic clicks were confirmed (via `GetForegroundWindow`) to be landing elsewhere. Stopped immediately rather than continue clicking blindly; worth a manual look next time the app is open.

## Settings: emoji removed (2026-09-01)

Swept the last pattern — Governance, Trust Boundary, Diagnostics, Settings, and Orchestration. Only Orchestration had emoji: `ModeIcon()` returned one of three C# unicode escapes (`\U0001F465` people, `⚡` bolt, `\U0001F41D` bee) for the Sequential/Parallel/Swarm run-mode badges, plus one hardcoded bee on the "Swarm Defaults" badge. The other four screens, and Orchestration's Desktop counterpart, were already clean — Desktop's run-mode picker is a plain-text `ComboBox` with no icons at all.

Fixed on Web only (nothing to fix on Desktop): Sequential is now three horizontal lines (a "steps in order" glyph), Parallel is three vertical lines, Swarm reuses the same package icon from the Chat/System-Integrations passes. All three are plain straight-line geometry — no arcs, zero hand-drawing risk. `ModeIcon()` now returns inline SVG markup rendered via `MarkupString`, the same pattern used for Chat's suggestion tiles. Verified live in Chrome: both the team-list badge and the detail-header badge render the new icon correctly.

This closes out the pattern-by-pattern sweep from the review two turns back (Conversation → Overview → Browse → Settings).

## Cleanup pass: deferred converter + Login bugs (2026-09-01)

Closed out everything the sweep had left open:

- **Shared `BoolToLockIconConverter` (Desktop).** Now returns the `IconLock`/`IconUnlock` `StreamGeometry` from `NavIcons.axaml` (via the same `Application.Current.TryGetResource` pattern already used by `BoolToBrushConverter`/`NavActiveBrushConverter`), instead of 🔒/🔓 text. All three call sites updated to bind a `Path.Data` instead of a `TextBlock.Text`: `AgentsView.axaml`, and `ChatView.axaml`'s session-level privacy toggle (distinct from the message-avatar mark fixed in the Chat pass).
- **Web's `Chat.razor` had two more emoji this whole sweep missed**, caught while touching the file for the item above: the session-level privacy toggle (🔒/🔓, the Web twin of the Desktop fix) and the remember-form's "🔒 Private" checkbox label. Fixed the same way as their Dashboard/Memory/Agents equivalents. Also fixed `Chat.razor`'s error-banner ⚠, explicitly deferred at the end of the original Chat pass — closes that loop too.
- **`LoginWindow.axaml` (Desktop), the two bugs tracked since the original design review:** `Background="{DynamicResource BackgroundBrush}"` → `SurfaceBackground` (the old key doesn't exist in either theme file, so `DynamicResource` was failing silently and the window never got its themed background), and hardcoded `Foreground="Red"` → `{DynamicResource StatusFail}`.

Web verified live in Chrome (private toggle, remember-form checkbox — both render the new icon correctly on a fresh tab). Desktop builds clean; the `BoolToLockIconConverter`/`LoginWindow` fixes weren't re-verified live this round — same `SetForegroundWindow` automation limitation as the Browse pass, and the icon geometry itself was already pixel-confirmed from the Overview pass.

**Still open:** the ✕ typographic close/remove glyph (left alone everywhere, deliberately — not a pictorial-emoji violation), and the placeholder "S" mark artwork (next up).

## Production code touched by this work

Everything from the nav-mark cleanup through the pattern sweep, in commit order — 8 commits, 33 production files across both platforms, all pushed directly to `development` (this repo's normal workflow, not a deviation):

| Commit | What it shipped |
|---|---|
| `5038190`, `48ff6e2` | Nav mark/toggle cleanup — `MainLayout.razor`+CSS (Web), `MainWindow.axaml` (Desktop), both mocks |
| `1ad8c9d` | Chat: mark reuse + emoji removed — `Chat.razor`, `ChatMessage.razor` (Web); `ChatView.axaml` + 4 new `NavIcons.axaml` resources (Desktop) |
| `15fea93` | Overview: emoji removed — `UserDashboard.razor`, `CommandCenter.razor` (Web); `UserDashboardView.axaml`, `CommandCenterView.axaml` + both ViewModels (Desktop) |
| `c99920a` | Browse: emoji removed across 9 screens — `Artifacts`, `Documents`, `Memory`, `Agents`, `Projects`, `Admin`, `Workspaces`, `WorkspacesAdmin`, `SystemIntegrations`, `Integrations` (Web); `DocumentsView`, `MemoryView`, `SystemIntegrationsView`, `IntegrationsView` (Desktop) |
| `5e58296` | Settings: emoji removed — `Orchestration.razor` run-mode badges |
| `5b711ce` | Deferred converter + Login bugs — `BoolToLockIconConverter.cs`, `AgentsView.axaml`, `ChatView.axaml` (Desktop); more of `Chat.razor` (Web); `LoginWindow.axaml`'s two theming bugs |
| `bbeead6` | Web favicon — `App.razor`, new `favicon.ico` |

Every commit above was scoped to the emoji/mark-cleanup review from `docs/design/README.md`'s own findings — nothing unrelated rode along. Full readout available via `git log --oneline 3484766..HEAD` (`3484766` is the two-file mock consolidation this log starts counting from).

## Open decisions

- **Login theme on a fresh machine.** `App.razor` hardcodes `data-theme="dark"` and JS overrides from `localStorage`. Login renders before user preferences load, so it shows whatever the browser last stored — decide whether it should follow `prefers-color-scheme`.

## Already shipped from this work

- The left-nav redesign (commit `7970ca3`): collapsible rail, real line icons replacing emoji, left accent bar for the active item, Admin's nine destinations grouped under Overview / Access / Safety / System.
- Web's `.rail-icon` dropped 42px → 40px to match Desktop, closing the one real parity gap.
