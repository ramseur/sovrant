# Design

This folder is the design record for Sovrant's user-facing surfaces. It exists to let anyone — human or agent — **verify a design before and after it ships**, without running the app.

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

## Open decisions

- **The mark is a letter, not a logo.** An "S" in a rounded purple square. It's now the only brand element in the shell, and at 54px it's the first thing a new user sees on Login. Worth drawing a real one.
- **Login theme on a fresh machine.** `App.razor` hardcodes `data-theme="dark"` and JS overrides from `localStorage`. Login renders before user preferences load, so it shows whatever the browser last stored — decide whether it should follow `prefers-color-scheme`.

## Bugs this pass found, not yet fixed

- `LoginWindow.axaml` binds `Background="{DynamicResource BackgroundBrush}"` — a key defined in neither `SovrantDarkColors.axaml` nor `SovrantLightColors.axaml`. `DynamicResource` fails silently, so the Desktop login window never receives its themed background. Should be `SurfaceBackground`.
- The same file hardcodes `Foreground="Red"` for error text instead of the `StatusFail` token, so it ignores the theme.

## Already shipped from this work

- The left-nav redesign (commit `7970ca3`): collapsible rail, real line icons replacing emoji, left accent bar for the active item, Admin's nine destinations grouped under Overview / Access / Safety / System.
- Web's `.rail-icon` dropped 42px → 40px to match Desktop, closing the one real parity gap.
