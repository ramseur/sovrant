# Design

This folder is the design record for Sovrant's user-facing surfaces. It exists to let anyone — human or agent — **verify a design before and after it ships**, without running the app.

**Scope note:** most entries below describe changes actually applied to production code (`src/Sovrant.Web`, `src/Sovrant.Desktop`), committed and pushed to `development` — not just edits to `web.html`/`desktop.html`. The two mock files stay the source of truth for what a screen *should* look like; the dated sections are the log of real code being brought in line with them. See "Production code touched by this work" below for the full commit-by-commit list.

## Two files. That's the whole thing.

- **`web.html`** — every Web screen.
- **`desktop.html`** — every Desktop screen.

Open either in a browser; no build step. Pick a screen from the index on the left. Each entry is tagged with the pattern that renders it.

A third file will join them when the CLI gets a real design pass. Today it's out of scope — the README calls it "functional but actively being refined".

## 23 screens, 5 patterns

The destinations behind the nav are not 23 designs. They're five patterns plus data:

| Pattern | Screens | What it is |
|---|---|---|
| **Browse** | 15 | Searchable list beside a detail pane. Artifacts, Code Templates, Documents, Memory, Skills, Tools, Agents Library, Orchestration, Workflows, Projects, Users, Workspaces, Providers, Platform Integrations, System Integrations. |
| **Overview** | 2 | Stat tiles over an activity table. Dashboard (scoped to you), Command Center (scoped to everyone). |
| **Settings** | 4 | Sectioned cards of labelled rows, each row one control plus the sentence explaining it. Settings, Governance, Trust Boundary, Diagnostics. |
| **Conversation** | 1 | Chat. Genuinely its own shape — welcome state, thread, collapsed work strips, composer. |
| **Entry** | 1 | Login. The only screen with no rail. |

(Was 21/13/4 until 2026-09-01 — Orchestration existed as its own nav destination the whole time but was never counted in the running total, on top of being mis-tagged Settings. Was 22/14 until 2026-09-04 — Workflows added as a new screen, see below.)

This is the point of the folder. Those pages were each built standalone — they share no layout classes today, which is exactly why they drift. Designing the pattern once and treating each screen as pattern + data is what stops it.

**When adding a screen, use an existing pattern.** A new pattern needs a reason the existing four can't express it.

## Orchestration was mis-tagged Settings (fixed 2026-09-01)

Caught by inspection, not code — Orchestration was rendering as a bare Settings screen (two sections, "Team run profile" and "Swarm") with no team list, no Run panel, no Members. The real page (`Orchestration.razor`) is a full Browse screen: a searchable team list on the left, and a detail pane with three sections (Run, Run Profile, Members) instead of Browse's usual single key/value block. The extra section count is what made it read as Settings — the shell was Browse the whole time.

Fixed in the mock only (design-only pass, no `src/` changes): `S.orchestration` now carries a real team (`test` · Parallel · 1 agent) and renders through `browseHTML()` like every other list screen, with a dedicated detail-pane renderer (`orchTeamDetailHTML`) for the three-section content. "Swarm Defaults" — a real second view of this same screen in the actual product, not a separate destination — is wired up as a **Team / Defaults** toggle next to Chat's existing Thread/Welcome toggle (both now share one generalized `#screenToggle` control instead of a Chat-only one). Two new line icons (`I.seq` three horizontal lines, `I.par` three vertical lines) join the existing package icon for the Sequential/Parallel/Swarm mode badge — matching the icons already shipped in real `Orchestration.razor` during the emoji-cleanup pass.

Not modeled in the mock, deliberately: the "New Team" and "Add Member" inline forms. Those are transient interaction states (open a form, fill it, submit), not distinct screens — same reasoning as why the mock doesn't model a loading spinner or a validation-error state for every button elsewhere.

**Fixed (2026-09-01):** the ⚙ gear emoji on both platforms' "Swarm defaults" button — `Orchestration.razor:16` (`&#x2699;`) and its exact twin on Desktop, `OrchestrationView.axaml:15` (also `&#x2699;`, missed by the same earlier sweep since it never actually opened this file). Both now use the same package/box icon as "Swarm Defaults" everywhere else — reused directly on Web, and via the existing `IconSwarm` `StreamGeometry` on Desktop. Kept the existing gear-button interaction (still opens the same Swarm Defaults panel via `ToggleSwarmConfig`/`ToggleSwarmConfigCommand`) rather than also swapping it for the mock's Team/Defaults toggle — that's a bigger interaction change than "fix the emoji" asked for, still worth doing as its own follow-up. Verified live: button renders the box icon correctly and still opens the real Swarm Defaults panel.

**Fixed (2026-09-02):** the deferred follow-up above — the single gear button is now the mock's actual **Team / Defaults** toggle on both platforms, matching the mock exactly. `Orchestration.razor`: `ToggleSwarmConfig()` split into `ShowTeamView()`/`ShowDefaultsView()`; header now shows `Team`/`Defaults`/`+ New` using the existing `.btn-sm.btn-primary`/`.btn-outline` classes (filled when active, outlined otherwise), Defaults panel's Close button now calls `ShowTeamView`. `OrchestrationViewModel.cs`: same split (`ToggleSwarmConfigCommand` → `ShowTeamViewCommand`/`ShowDefaultsViewCommand`); `OrchestrationView.axaml` header/drawer-Close buttons rebound accordingly. Desktop has no CSS classes to reuse, so the active/inactive fill is done with `BoolToTabBrushConverter` (`Converters/BoolToTabBrushConverter.cs`) — extended in this pass from a single hardcoded BrandPrimary/Transparent background converter into a parameterized one (`ActiveResource`/`InactiveResource`), plus a new `.Foreground` instance (white on the filled tab, `TextPrimary` otherwise), mirroring the existing `NavActiveBrushConverter` pattern already used for sub-nav active states. Caught and fixed one bug surfaced by this: the converter looked up resources via `app.Styles.TryGetResource`, which can't see resources declared in `Application.Resources` (where `BrandPrimary`/`TextPrimary` actually live) — silently fell through to transparent for both buttons. Fixed to `app.TryGetResource`, matching the working pattern in `NavActiveBrushConverter`. Verified live on Web (Chrome); verified live on Desktop up through the button restyle (Team filled/Defaults outlined, both readable) before window-focus contention from another app on screen interrupted further click-through — the click path itself is unchanged from the already-verified `ShowSwarmConfig` binding the old gear button used, so not re-litigated further.

Verified live in Chrome, both platforms: team list renders with the mode badge, Run/Run Profile/Members all present in the detail pane, Team/Defaults toggle switches correctly, parity holds at 49 changed lines (chrome-only, confirmed line-by-line).

## Parity

**Web and Desktop should match as closely as each platform allows.** `desktop.html` is generated from `web.html` with only the platform chrome swapped — browser tab strip and address bar become a native titlebar and window controls. A diff of the two files should show *only* those chrome lines, the title/heading, and the theme storage key. Anything else that differs is drift.

Current state: 49 changed lines, all accounted for by that list.

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

**Still open:** the ✕ typographic close/remove glyph (left alone everywhere, deliberately — not a pictorial-emoji violation).

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

## Screen-by-screen visual pass (2026-09-01)

Went through all 5 patterns in the browser — Login, Dashboard (Overview), Chat, Artifacts and Users (Browse), Orchestration and Diagnostics (Settings, as tagged at the time) — checking for genuine design roughness now that the emoji/mark sweep is done: layout, spacing, hierarchy, semantic color use. Nothing found worth changing. The pattern-once approach is holding: every screen checked reads as the same design system, not a bespoke one-off. Not touching the other 14 screens individually — they render through the same 5 pattern functions already confirmed clean, so per-screen re-verification would be checking the same code path repeatedly, not new risk.

(Orchestration's Settings tag turned out to be wrong — see "Orchestration was mis-tagged Settings" above, caught the same day by inspection rather than by this pass. The design-roughness check above still stands for what it actually looked at: Orchestration's *content* — sectioned controls, clear labels — was fine, it was the pattern classification and missing team-list/Run/Members content that were wrong.)

That leaves the two long-open items below actually resolved instead of just tracked.

## New screen: Workflows (2026-09-04, design-only)

Roadmap Phase 129 ("Missions → Workflows") shipped its rename in real code first this round — full-stack, not the presentation-layer-only scoping the roadmap entry originally described (see `docs/roadmap.md` Phase 129's 2026-09-03 update for why that changed) — plus a new `WorkflowSchedulerService` that advances `Planning`/`Running` workflows on its own, without a human or agent explicitly asking. This entry is the design-only follow-up: a dedicated **Workflows** screen so there's somewhere to actually see that happening, mocked here first per this repo's usual process — no `src/` changes yet.

**Nav placement:** under Agents, alongside Orchestration (`agentlib → orchestration → workflows`) — same substrate, same group. Pattern: Browse, same shell as every other list screen in that group.

**List pane:** goal (truncated), a status badge, and a relative timestamp + owner — three sample rows covering `Running`, `Awaiting human`, and `Completed` so the badge palette is visible in one screen. New `.badge.b-fail` CSS rule added (using the existing `--fail` token, same recipe as `.b-ok`/`.b-warn`) for a future `Failed` state — not used by the three samples shown, but the real `WorkflowStatus` enum has it and the badge needed to exist before real code reaches for it.

**Detail pane** (`workflowDetailHTML`): goal in full, status badge, a **Plan** section (numbered steps with intent/expected-outcome/tier, straight from `plan_json`'s shape), an **Event journal** (timestamp + event type, exactly what `/workflow events` already returns as text — rendered as a list here), and actions that adapt to status: `Run now` when idle, `Resume` when `Awaiting human`, both hidden once terminal (`Completed`/`Failed`/`Cancelled`) since there's nothing left to run; `Cancel` hidden once terminal for the same reason; `Export` always available. This mirrors Orchestration's own "richer than a generic Browse detail" shape (multiple sections instead of one key/value block) — not a new pattern, same reasoning as why Orchestration's detail pane already looks like this.

**Copy note on the Run button:** now that the scheduler exists, a manual "Run" is a *force it now* / *resume from a pause* action, not the only way anything happens — the note on the screen says this explicitly so it doesn't read as a regression when someone notices workflows moving without being clicked.

**Also fixed while in this file:** two mock strings still said "mission" (Chat's welcome subtitle and one suggestion tile) — stale now that the real code shipped as "workflow" this round. Dashboard's and Command Center's stat tiles (`My missions` / `Missions`) had the same staleness, same fix.

Not modeled here, deliberately (same reasoning as Orchestration's "New Team"/"Add Member" forms): the "New Workflow" creation form. It's a transient interaction state, not a distinct screen.

Verified in Chrome (list renders all three sample rows and badge colors correctly; detail pane renders goal/plan/journal for the `Running` sample) and via direct console evaluation of `workflowDetailHTML()` against the `Awaiting human` and `Completed` samples (button visibility confirmed correct for both) — the mock's `sel` is always `items[0]`, same static-first-item limitation Orchestration's mock already has, so the other two states aren't reachable by clicking, only by calling the render function directly. `web.html`/`desktop.html` diff-checked: the added `S.workflows` block and `workflowDetailHTML` function are byte-identical between the two files.

**Shipped to real code (2026-09-04):** `Workflows.razor` (Web) and `WorkflowsView.axaml`/`WorkflowsViewModel.cs` (Desktop), nav-wired into `AgentsPanel.razor`/`AgentsPanelView.axaml` and `MainWindow.axaml`'s DataTemplate registry, matching the mock's shape — goal, status badge, plan steps parsed from `plan_json`, event journal, and status-adaptive actions (Run now/Resume, Cancel, Export — all hidden once terminal). Both platforms call `IWorkflowStore`/`IWorkflowExecutor`/`WorkflowExportService` directly (in-process DI), the same pattern `Orchestration.razor`/`OrchestrationViewModel.cs` already use — no new backend surface needed. Reused `CommandCenter`'s existing `.cc-status`/`.cc-status-*` badge classes on Web instead of inventing new CSS (added one line extending `.cc-status-completed` to the green/pass treatment, since no rule previously existed for that state).

Caught two real bugs during implementation, unrelated to the page itself but surfaced by working through the code: `WorkflowExportService`'s doc comment and `PrivacyRoutes.cs`'s actual route mapping both still referenced `/v1/missions/...` — the export doc comment was cosmetic, but the `PrivacyRoutes.cs` one was a live orphaned endpoint (`PATCH /v1/missions/{id}/privacy` instead of `/v1/workflows/{id}/privacy`) left over from the rename. Both fixed.

Verified live end-to-end on Web in Chrome: created a real workflow, confirmed it appears in the list with the correct status badge, confirmed the detail pane renders goal/empty-plan/event-journal correctly, cancelled it and confirmed the status transition, the event-journal append, and the action buttons correctly disappearing once terminal. Desktop builds clean (0 errors, no new warnings) and mirrors Web's exact binding shape, but live screenshot verification wasn't completed — the window was covered by what appeared to be an active screen-share on the user's machine, and capturing that screen region risked grabbing meeting content, so verification stopped there rather than risk it.

## Open decisions

- **Login theme on a fresh machine — resolved.** `App.razor` hardcodes `data-theme="dark"` on the `<html>` tag before any JS runs; an explicit `data-theme` stamp always wins over `prefers-color-scheme` in CSS, so a first-time visitor never sees their actual OS preference regardless of what it is. The mock already does this correctly — `web.html`/`desktop.html` never stamp `data-theme` until the viewer explicitly picks Light/Dark, so `@media (prefers-color-scheme)` decides on first paint. **Decision: match the mock — stop hardcoding `data-theme="dark"` in `App.razor`; leave it unset until `localStorage` has a stored choice.** Not implemented yet (design-only pass); real fix is a one-line removal in `App.razor` plus the equivalent JS-sets-before-first-paint check already in place for the stored-preference case.

- **Control height scale — documented, not unified.** Flagged early in this pass as ad hoc (9 distinct heights: 22/26/28/29/30/32/34/36/38/40/44px). Audited every real height in `web.html`'s CSS (excluding the browser-chrome mockup frame, which isn't product UI) and it's looser than ideal but not random — it clusters into three real tiers:
  - **28–32px** — compact/icon-only controls: `.chip` (28), `.av` avatar (29), `.ib` icon button (30), `.send` composer action (32), `.ctog` rail toggle (26, deliberately smaller — floats half-off the rail edge)
  - **34–36px** — standard controls: `.btn` secondary button, `.inpb` settings input, `.idx` index row (34), `.search` (36)
  - **40–44px** — primary/high-traffic controls: `.nav` rail row, `.li` login input (40), `.frow` footer row, `.lbtn` login primary button (44)

  Not forcing a mass rewrite to 3 exact values — every current height was visually tuned for its specific control, and normalizing ~15 CSS rules with no way to re-verify each one visually within this pass is a real regression risk for a cosmetic-only win. **Standard going forward: new controls should land on 28, 32, 34, 36, 40, or 44px** — one of the six values already in use — rather than introduce a 7th.

## Already shipped from this work

- The left-nav redesign (commit `7970ca3`): collapsible rail, real line icons replacing emoji, left accent bar for the active item, Admin's nine destinations grouped under Overview / Access / Safety / System.
- Web's `.rail-icon` dropped 42px → 40px to match Desktop, closing the one real parity gap.
