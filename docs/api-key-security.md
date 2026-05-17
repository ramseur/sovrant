# API Key Security

## How LLM API Keys Are Handled

Sovrant operates in two modes with different security characteristics.

### Remote Mode (Web or Desktop → Sovrant Server)

The client authenticates to the Sovrant server using a session token (`svt_*`). The server holds LLM provider credentials in its own encrypted keystore and sends them directly to OpenAI, OpenRouter, or other providers. The client never sees or transmits the LLM API key. This is the recommended production deployment.

```
Client  →  svt_* token  →  Sovrant Server  →  LLM API key (HTTPS)  →  Provider
```

### Embedded Mode (Desktop, Local Runtime)

The LLM API key is stored in the local encrypted keystore (`~/.sovrant/credentials/.keystore`). On each turn, the key is read from the store and sent to the LLM provider over HTTPS. No intermediate server is involved — the key travels only between the local machine and the provider.

```
Desktop  →  LLM API key (HTTPS, direct)  →  Provider
```

This is the same pattern used by every LLM desktop client (Cursor, Claude Desktop, etc.). Third-party LLM providers require a Bearer token on every HTTP request; there is no reusable pre-authenticated session mechanism available.

## Why the Key Is Sent Per-Request

The key is fetched from the credential store and attached to each HTTP request inside `OpenAiCompatProvider.BuildRequestAsync`. This is intentional:

- Key rotations in Settings take effect immediately without a restart.
- The key is never held in a long-lived HTTP client header where it could be inspected by other parts of the process.
- It keeps the auth flow explicit and auditable at the call site.

## Storage

| Mode | Key stored in | Encrypted |
|---|---|---|
| Embedded desktop | `~/.sovrant/credentials/.keystore` | Yes (DPAPI on Windows) |
| Remote server | Server-side credential store | Yes |
| Web client | Never stored on client | N/A |

## What Is Not a Security Issue

Passing the API key on every HTTPS request to the LLM provider is not a vulnerability. TLS encrypts the request in transit. The key is not logged, not passed to any Sovrant-controlled intermediary in embedded mode, and not exposed to other users in remote mode.
