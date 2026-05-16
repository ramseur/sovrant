# SSL / TLS Configuration

Sovrant supports HTTPS on both the **Web** frontend and the **Server** (which also hosts the MCP endpoint). TLS is opt-in and configured entirely through environment variables — no config file changes required.

## How it works

When `SOVRANT_TLS_CERT` is set to a valid certificate file, Kestrel opens a second listener on the HTTPS port alongside the plain HTTP port. HTTP traffic is still accepted (and redirected to HTTPS when TLS is active).

The MCP server runs inside `Sovrant.Server` and inherits its TLS configuration automatically — there is no separate MCP TLS setup.

## Environment variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `SOVRANT_TLS_CERT` | For TLS | — | Path to a `.pfx` certificate, or a `.pem`/`.crt` certificate when used with `SOVRANT_TLS_KEY` |
| `SOVRANT_TLS_KEY` | PEM only | — | Path to the PEM private-key file. Only needed when `SOVRANT_TLS_CERT` is a `.pem`/`.crt` |
| `SOVRANT_TLS_CERT_PASSWORD` | PFX only | — | Passphrase for a PFX certificate. Omit when using separate PEM files |
| `SOVRANT_TLS_HTTPS_PORT` | No | `5101` (Web), `5443` (Server) | HTTPS listener port |

TLS is **disabled** when `SOVRANT_TLS_CERT` is not set or points to a file that does not exist.

---

## Option A — Self-signed certificate (development)

Use the .NET dev-certs tool for local HTTPS. This is the fastest path and trusted automatically in browsers on the same machine.

```bash
dotnet dev-certs https --export-path ./certs/dev.pfx --password devpassword
dotnet dev-certs https --trust
```

Then start the server with:

```bash
# Sovrant.Server
SOVRANT_TLS_CERT=./certs/dev.pfx \
SOVRANT_TLS_CERT_PASSWORD=devpassword \
dotnet run --project src/Sovrant.Server

# Sovrant.Web (embedded mode)
SOVRANT_TLS_CERT=./certs/dev.pfx \
SOVRANT_TLS_CERT_PASSWORD="<your-pfx-passphrase>" \
dotnet run --project src/Sovrant.Web
```

On Windows (PowerShell):

```powershell
$env:SOVRANT_TLS_CERT = ".\certs\dev.pfx"
$env:SOVRANT_TLS_CERT_PASSWORD = "<your-pfx-passphrase>"
dotnet run --project src/Sovrant.Server
```

Default ports: Web on `5101`, Server/MCP on `5443`.

---

## Option B — PEM certificate (production / Let's Encrypt)

Let's Encrypt and most CAs issue PEM files (`fullchain.pem` + `privkey.pem`). Point the two env vars directly at the files — no conversion to PFX needed.

```bash
SOVRANT_TLS_CERT=/etc/letsencrypt/live/yourdomain.com/fullchain.pem \
SOVRANT_TLS_KEY=/etc/letsencrypt/live/yourdomain.com/privkey.pem \
SOVRANT_TLS_HTTPS_PORT=443 \
dotnet run --project src/Sovrant.Server
```

> **Note:** Binding to port 443 requires elevated privileges on Linux. Either run as root, use `setcap`, or put a reverse proxy in front (see Option C).

To renew automatically with Certbot, add a deploy hook that restarts the service:

```bash
# /etc/letsencrypt/renewal-hooks/deploy/sovrant.sh
systemctl restart sovrant-server
```

---

## Option C — Reverse proxy (recommended for production)

Running Nginx or Caddy in front of Sovrant is the simplest production setup. The proxy terminates TLS; Sovrant runs plain HTTP internally and never needs a certificate.

### Nginx

```nginx
server {
    listen 443 ssl;
    server_name yourdomain.com;

    ssl_certificate     /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;

    # Web frontend
    location / {
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

server {
    listen 443 ssl;
    server_name api.yourdomain.com;

    ssl_certificate     /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;

    # Server + MCP endpoint
    location / {
        proxy_pass http://127.0.0.1:5200;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Caddy (automatic HTTPS)

```caddy
yourdomain.com {
    reverse_proxy 127.0.0.1:5100
}

api.yourdomain.com {
    reverse_proxy 127.0.0.1:5200
}
```

Caddy handles certificate issuance and renewal automatically with no further configuration.

---

## Forwarded headers

When running behind a reverse proxy, enable forwarded-header processing so Sovrant sees the real client IP and scheme. This is already wired up in `Sovrant.Web` and `Sovrant.Server` — it activates automatically when a proxy is detected via the `X-Forwarded-Proto` header.

No extra configuration is needed.

---

## Ports reference

| Service | HTTP | HTTPS (default) |
|---|---|---|
| `Sovrant.Web` | 5100 | 5101 |
| `Sovrant.Server` + MCP | 5200 | 5443 |

Override HTTPS ports with `SOVRANT_TLS_HTTPS_PORT`. HTTP ports are fixed and cannot be changed via env vars.
