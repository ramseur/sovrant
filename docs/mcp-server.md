# MCP Server Mode

Sovrant can run as an **MCP (Model Context Protocol) server**, allowing MCP-aware IDEs and agents to use it as a tool provider. This is separate from the HTTP server — MCP uses **stdio transport** (JSON-RPC 2.0 over stdin/stdout pipes), so there are zero port conflicts with `Sovrant.Server` on port 5200.

---

## Activation

MCP server mode is **opt-in only** — it never runs unless you explicitly launch it:

```bash
export LLM_API_KEY="sk-..."
sovrant mcp-server
```

Or with `dotnet run`:

```bash
dotnet run --project src/Sovrant.Cli -- mcp-server
```

The process blocks on stdin, waiting for JSON-RPC messages from the connected IDE. It exits when stdin closes (IDE disconnects).

**No performance overhead:** When not using `mcp-server`, no MCP services are registered, no code paths execute, and no resources are consumed.

---

## IDE Configuration

### VS Code (GitHub Copilot)

Add to `.vscode/settings.json`:

```json
{
  "github.copilot.chat.mcpServers": {
    "sovrant": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/Sovrant.Cli", "--", "mcp-server"],
      "env": {
        "LLM_API_KEY": "sk-..."
      }
    }
  }
}
```

### Cursor

Add to `.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "sovrant": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/Sovrant.Cli", "--", "mcp-server"],
      "env": {
        "LLM_API_KEY": "sk-..."
      }
    }
  }
}
```

### Windsurf

Add to `~/.codeium/windsurf/mcp_config.json`:

```json
{
  "mcpServers": {
    "sovrant": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/Sovrant.Cli", "--", "mcp-server"],
      "env": {
        "LLM_API_KEY": "sk-..."
      }
    }
  }
}
```

### Claude Code

Add to `.claude/mcp.json`:

```json
{
  "mcpServers": {
    "sovrant": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/Sovrant.Cli", "--", "mcp-server"],
      "env": {
        "LLM_API_KEY": "sk-..."
      }
    }
  }
}
```

> **Tip:** If you've published Sovrant as a single binary, replace `"dotnet", "run", "--project", "path/to/src/Sovrant.Cli", "--"` with just `"sovrant"`.

---

## Available Tools

All tools registered in `IToolRegistry` are exposed via MCP, plus a synthetic `chat` tool.

### Sovrant Tools

All 50 standard tools are available:

**File:** `Read`, `Write`, `Edit`, `Glob`, `Grep`, `LS`
**Shell:** `Bash`, `PowerShell`, `REPL`
**Web:** `WebFetch`, `WebSearch`
**Task Management:** `TodoWrite`, `TaskCreate`, `TaskGet`, `TaskList`, `TaskOutput`, `TaskStop`, `TaskUpdate`
**Agent & Interaction:** `Agent`, `AskUserQuestion`, `Sleep`
**Plan & Worktree:** `EnterPlanMode`, `ExitPlanMode`, `EnterWorktree`, `ExitWorktree`
**Team Orchestration:** `TeamCreate`, `TeamDelete`, `TeamStatus`, `TeamDelegate`, `TeamRun`, `TeamPublish`
**Missions:** `Mission`
**Swarm:** `Swarm`, `SwarmStatus`
**Discovery & Skills:** `ToolSearch`, `Skill`, `SkillCreate`
**Quality:** `Verify`
**MCP:** `ListMcpResources`, `ReadMcpResource`, `MCPTool`, `McpAuth`
**LSP:** `LspHover`, `LspDefinition`, `LspReferences`, `LspDiagnostics`, `LspRename`
**Notebook:** `NotebookEdit`

### `chat` Tool

A synthetic MCP tool that runs a full Sovrant agentic turn — the IDE sends a message, Sovrant runs it through the LLM with the full tool loop, and returns the final response.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `message` | string | Yes | The user message to send to the agentic loop |
| `session_id` | string | No | Session ID to resume a previous conversation |

The `chat` tool always passes the tool filter — it cannot be excluded.

---

## Available Resources

| URI | Description | MIME Type |
|---|---|---|
| `sovrant://sessions` | List of all session IDs with at least one entry | `application/json` |
| `sovrant://config` | Current Sovrant runtime configuration | `application/json` |
| `sovrant://sessions/{id}` | Full message history for a specific session | `application/json` |

---

## Tool Filtering

By default, all tools are exposed. Set `SOVRANT_MCP_TOOLS` to a comma-separated list to restrict which tools are available:

```bash
# Only expose file tools and chat
export SOVRANT_MCP_TOOLS="Read,Write,Edit,Glob,Grep,LS"
sovrant mcp-server
```

The `chat` tool is always available regardless of the filter.

Tool names are **case-sensitive** — use the exact names as shown above.

---

## Authentication

MCP uses stdio (pipes), not HTTP — so there are no `Authorization` headers. Authentication works as a **startup gate**: the server validates the token before accepting any JSON-RPC messages.

### How to enable

**Step 1 — set the required token on the server side:**
```bash
export SOVRANT_MCP_TOKEN="your-secret-token"
```

This is the token the server expects. Keep it secret (use a secrets manager, `.env` file with restricted permissions, or a system keychain).

**Step 2 — pass the token in your IDE config via `--token`:**

```json
{
  "command": "sovrant",
  "args": ["mcp-server", "--token", "your-secret-token"],
  "env": {
    "LLM_API_KEY": "sk-..."
  }
}
```

**Behavior:**

| `SOVRANT_MCP_TOKEN` | `--token` provided | Result |
|---|---|---|
| Not set | Any | Allowed (open mode) |
| Set | Not provided | Process exits, error to stderr |
| Set | Wrong value | Process exits, error to stderr |
| Set | Correct value | Allowed |

Errors go to **stderr** so they don't corrupt the stdout JSON-RPC transport. The IDE will see the process exit immediately with a non-zero code.

### VS Code example with token

```json
{
  "github.copilot.chat.mcpServers": {
    "sovrant": {
      "command": "sovrant",
      "args": ["mcp-server", "--token", "your-secret-token"],
      "env": {
        "LLM_API_KEY": "sk-..."
      }
    }
  }
}
```

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `LLM_API_KEY` | Yes | API key for the LLM provider |
| `LLM_BASE_URL` | No | Provider base URL (default: OpenAI) |
| `SOVRANT_MCP_TOKEN` | No | Required bearer token. If set, callers must pass `--token <value>` matching this. Unset = no auth required. |
| `SOVRANT_MCP_TOOLS` | No | Comma-separated allow-list of tool names. Unset = all tools. |

All standard Sovrant environment variables (`ROUTER_MODE`, `ROUTER_STRATEGY`, `LLM_WEB_SEARCH`, etc.) are respected.

---

## Security

- **Token authentication** — `SOVRANT_MCP_TOKEN` + `--token` provides startup-time auth. Mismatches exit the process before any JSON-RPC exchange.
- **Permission mode** is forced to `DontAsk` — all tool executions are auto-approved. MCP server mode is non-interactive; there is no console to prompt.
- **Console logging is suppressed** — stdout is the JSON-RPC transport. Logs go to file only (`~/.sovrant/logs/`).
- **No HTTP exposure** — MCP runs over stdio pipes. The process is only accessible to the parent process (IDE) that spawned it.
- Use `SOVRANT_MCP_TOOLS` to restrict which tools are exposed if you want to limit what the IDE can do.

---

## MCP OAuth Authentication (McpAuth tool)

Some MCP servers require OAuth authorization before they can be used (e.g., GitHub MCP server requires a GitHub OAuth app token). Sovrant handles this through the `McpAuth` tool and a lightweight OAuth 2.0 Authorization Code + PKCE flow that runs through the Sovrant HTTP server.

### Prerequisites

- `Sovrant.Server` must be running on port 5200 (or `SOVRANT_PORT`)
- The OAuth app's redirect URI must be registered as `http://localhost:{SOVRANT_PORT}/v1/mcp/auth/callback`

### Configuration

Add `oauth_config` to an MCP server entry in `settings.json`:

```json
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "oauth_config": {
        "client_id": "your-github-oauth-app-client-id",
        "authorization_url": "https://github.com/login/oauth/authorize",
        "token_url": "https://github.com/login/oauth/access_token",
        "scopes": ["repo", "read:org"],
        "token_env_var": "GITHUB_TOKEN",
        "redirect_uri": "http://localhost:5200/v1/mcp/auth/callback"
      }
    }
  }
}
```

| Field | Required | Description |
|---|---|---|
| `client_id` | Yes | Your OAuth app's client ID |
| `client_secret` | No | Client secret (omit for public clients — PKCE is always used) |
| `authorization_url` | Yes | Provider's authorization endpoint |
| `token_url` | Yes | Provider's token endpoint |
| `scopes` | No | OAuth scopes to request |
| `token_env_var` | No | Env var to inject the access token into when reconnecting the MCP process |
| `redirect_uri` | No | Redirect URI override (default: `http://localhost:{SOVRANT_PORT}/v1/mcp/auth/callback`) |

### Flow

1. The model detects an MCP tool call fails with an auth error (or the user asks to authorize an MCP server).
2. The model calls the `McpAuth` tool with `{ "server": "github" }`.
3. Sovrant generates an authorization URL (PKCE challenge included) and returns it as text.
4. The model surfaces the URL to the user: _"Please visit this URL to authorize GitHub access: https://github.com/..."_
5. The user visits the URL, approves access in their browser.
6. GitHub redirects to `http://localhost:5200/v1/mcp/auth/callback?code=...&state=...`
7. Sovrant exchanges the code for an access token, stores it encrypted in `~/.sovrant/credentials/`.
8. If `token_env_var` is set, Sovrant reconnects the MCP server process with the token injected as that environment variable.
9. The user sees a success page and closes the tab — no further action needed.

### Credential storage

Tokens are stored AES-256-GCM encrypted in `~/.sovrant/credentials/`. The master encryption key lives in `~/.sovrant/credentials/.keystore` (user-only permissions on POSIX). Tokens are **never** written to session history, logs, or any other file.

### Security notes

- PKCE (`code_challenge_method=S256`) is always used, even when a `client_secret` is present.
- OAuth state parameters expire after **10 minutes** — attempts to replay a callback after expiry are rejected.
- The `/v1/mcp/auth/callback` endpoint is exempt from bearer token authentication (the OAuth provider redirects there without credentials). The CSRF `state` parameter is the security control.
- The callback endpoint is only accessible if `Sovrant.Server` is running locally — it is not exposed to the internet.

---

## How It Works

1. The IDE starts `sovrant mcp-server` as a child process.
2. Sovrant registers all tools from `IToolRegistry` and exposes them as MCP tools via the `ModelContextProtocol` library.
3. The IDE sends JSON-RPC requests over stdin (`tools/list`, `tools/call`, `resources/list`, `resources/read`).
4. Sovrant processes each request and writes JSON-RPC responses to stdout.
5. Tool calls are dispatched to the same handlers used by the CLI and HTTP server.
6. The `chat` tool creates a transient `ConversationRuntime` and runs a full agentic turn — the IDE gets the agent's complete response including any tool use.

The MCP server uses the same `Sovrant.Runtime` and `Sovrant.Tools` infrastructure as the CLI and HTTP server. There is no separate tool implementation — everything is bridged through `IToolRegistry`.
