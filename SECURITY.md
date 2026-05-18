# Security Policy

## Supported Versions

| Version | Supported |
|---|---|
| 0.9.x (current preview) | ✅ |
| < 0.9.0 | ❌ |

## Reporting a Vulnerability

**Do not open a public GitHub issue for security vulnerabilities.**

Report security issues by email to **solutions@anant.us**. Include:

- A description of the vulnerability and its potential impact
- Steps to reproduce or a proof-of-concept (if safe to share)
- The version of Sovrant affected
- Any suggested mitigations you have identified

**Response targets:**
- Acknowledgement: within 48 hours
- Initial assessment: within 5 business days
- Fix or mitigation plan: within 30 days for critical issues

We will coordinate disclosure timing with you and credit you in the release notes unless you prefer to remain anonymous.

## Scope

The following are in scope:

- `Sovrant.Server` — HTTP server, authentication, route handlers
- `Sovrant.Runtime` — agent loop, tool execution, credential store
- `Sovrant.Tools` — all built-in tools (command injection, path traversal, SSRF)
- `Sovrant.Web` / `Sovrant.Desktop` — XSS, credential exposure, auth bypass
- `Sovrant.Mcp` — MCP protocol handlers
- TypeScript SDK — credential handling, SSE stream integrity

The following are **out of scope** for this policy:

- Vulnerabilities in underlying LLM providers (OpenAI, Anthropic, etc.)
- Issues requiring physical access to the host machine
- Social engineering attacks
- Theoretical issues with no practical exploit path

## Known Security Architecture Decisions

- API keys and credentials are stored exclusively in an AES-256-GCM encrypted keystore (`~/.sovrant/credentials/`). They are never written to `.env` files or environment variables.
- LLM API keys are sent per-request directly to the provider over HTTPS. In remote mode the client never sees the key — the server holds and sends it. See [`docs/security-architecture.md`](docs/security-architecture.md) for full details.
- The `WriteFileTool` enforces absolute paths only and blocks writes inside the artifact store root.
- The `LocalArtifactStore` enforces path containment via `ResolveAndGuard` — no path traversal is possible through the Artifact tool.
- Tool execution is permission-gated. Dangerous tools require explicit user approval unless `bypassPermissions` is set.
