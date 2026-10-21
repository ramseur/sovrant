# Sovrant.LoadTest

Load test for **embedded Sovrant.Web** (Phase 145 Part E). It drives the real thing — no test doubles:

1. Starts its own `Sovrant.Web` process on a throwaway database (port 5190 by default), unless you pass `--url`.
2. Creates the accounts (`load1@example.com` … `loadN@example.com`) through the runtime's identity service.
3. Signs each one in through the real sign-in form (antiforgery token, cookie).
4. Opens one **Blazor Server circuit** per person the way `blazor.web.js` does (`/_blazor` hub, `blazorpack` protocol, `StartCircuit` → `UpdateRootComponents`), acknowledges every render batch and answers browser calls.
5. Optionally clicks between pages (`/chat`, `/artifacts`, `/settings`, `/dashboard`, `/command`) while holding the circuits open.
6. Measures the Web process's memory — after forcing a full garbage collection, so the numbers are memory in use — before, while opening (in steps), after holding, and after everyone leaves.

It doesn't chat with a model: replies cost money and their memory belongs to the model provider. Concurrent chat turns are covered by `ConcurrentUsersTests` in `Sovrant.Runtime.Tests`.

## Run

```bash
dotnet build src/Sovrant.Web -c Release
dotnet run -c Release --project tools/Sovrant.LoadTest -- --users 300 --heap-limit-mb 1024
```

A report is written to `loadtest-results/` in the current directory.

| Option | Default | What it does |
|---|---|---|
| `--users N` | 100 | People to simulate |
| `--hold S` | 60 | Seconds to keep everyone connected |
| `--navigate-every S` | 15 | Average seconds between page changes per person (`0` = idle) |
| `--steps N` | 5 | Memory samples while opening (for the per-person slope) |
| `--concurrency N` / `--ramp N` | 20 / 20 | Sign-ins at once / new people per second |
| `--heap-limit-mb N` | — | Sets `DOTNET_GCHeapHardLimit`, like a container memory limit. A run that passes proves the server fits |
| `--gc server\|workstation` | ASP.NET default (server) | Garbage-collector mode |
| `--url URL` | — | Test an already-running Web instead (accounts `loadN@example.com` with the tool's password must exist; no memory numbers) |
| `--web PATH` | `src/Sovrant.Web/bin/<config>/net10.0/Sovrant.Web.dll` | Which Web build to start |
| `--configuration C` | Release | Build to start when `--web` isn't given |
| `--port P` | 5190 | Port for the Web it starts |
| `--out DIR` | `./loadtest-results` | Where reports go |
| `--keep-data` | off | Keep the throwaway data folder (database and `web.log`) |

Set `LOADTEST_TRACE_JS=1` to print every browser call the server makes and SignalR client warnings — useful when a new .NET version changes the circuit protocol.

## Reading the numbers

- **Without `--heap-limit-mb`, memory looks far bigger than it is.** Each sign-in hashes the password with Argon2id (64 MB for a moment), and on a machine with free RAM the .NET GC keeps that memory reserved instead of returning it. A heap snapshot of 50 connected people showed 31 MB of live objects while the process held over 2 GB. Use `--heap-limit-mb` (or a container limit in production) to see what the server really needs.
- **Sign-in time** is mostly Argon2id. At most one hash per CPU core (up to 8) runs at once, so a sign-in rush queues instead of exhausting memory.
- Closed circuits are kept for about 3 minutes so a reconnecting browser can resume, so memory after closing falls only gradually.

Results and sizing guidance: [docs/web-sizing.md](../../docs/web-sizing.md).
