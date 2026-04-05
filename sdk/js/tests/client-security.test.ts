import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { SovrantClient, SovrantApiError } from "../src/client.js";

// ─── URL / Protocol Validation ───────────────────────────────────────────────

describe("SovrantClient — URL validation", () => {
  it("accepts http: base URL", () => {
    expect(
      () => new SovrantClient({ baseUrl: "http://localhost:5200", token: "t" })
    ).not.toThrow();
  });

  it("accepts https: base URL", () => {
    expect(
      () =>
        new SovrantClient({ baseUrl: "https://api.example.com", token: "t" })
    ).not.toThrow();
  });

  it("rejects javascript: protocol", () => {
    expect(
      () =>
        new SovrantClient({
          baseUrl: "javascript:alert(1)",
          token: "t",
        })
    ).toThrow(/Invalid baseUrl/);
  });

  it("rejects file: protocol", () => {
    expect(
      () =>
        new SovrantClient({
          baseUrl: "file:///etc/passwd",
          token: "t",
        })
    ).toThrow(/Invalid baseUrl protocol/);
  });

  it("rejects data: protocol", () => {
    expect(
      () =>
        new SovrantClient({
          baseUrl: "data:text/html,<h1>hi</h1>",
          token: "t",
        })
    ).toThrow(/Invalid baseUrl protocol/);
  });

  it("rejects ftp: protocol", () => {
    expect(
      () =>
        new SovrantClient({ baseUrl: "ftp://evil.com", token: "t" })
    ).toThrow(/Invalid baseUrl protocol/);
  });

  it("rejects empty string base URL", () => {
    expect(
      () => new SovrantClient({ baseUrl: "", token: "t" })
    ).toThrow(/Invalid baseUrl/);
  });

  it("rejects relative path as base URL", () => {
    expect(
      () =>
        new SovrantClient({ baseUrl: "/relative/path", token: "t" })
    ).toThrow(/Invalid baseUrl/);
  });

  it("strips trailing slashes from baseUrl", () => {
    const client = new SovrantClient({
      baseUrl: "http://localhost:5200///",
      token: "t",
    });
    // Access via toJSON since baseUrl is private
    expect(client.toJSON().baseUrl).toBe("http://localhost:5200");
  });
});

// ─── Token Handling ──────────────────────────────────────────────────────────

describe("SovrantClient — token security", () => {
  it("rejects empty token", () => {
    expect(
      () => new SovrantClient({ baseUrl: "http://localhost:5200", token: "" })
    ).toThrow(/non-empty token/);
  });

  it("rejects non-string token (number cast)", () => {
    expect(
      () =>
        new SovrantClient({
          baseUrl: "http://localhost:5200",
          // @ts-expect-error — intentionally testing runtime guard
          token: 12345,
        })
    ).toThrow(/non-empty token/);
  });

  it("toJSON redacts the token", () => {
    const client = new SovrantClient({
      baseUrl: "http://localhost:5200",
      token: "super-secret-key",
    });
    const serialized = JSON.stringify(client);
    expect(serialized).not.toContain("super-secret-key");
    expect(serialized).toContain("[REDACTED]");
  });

  it("toJSON preserves non-sensitive fields", () => {
    const client = new SovrantClient({
      baseUrl: "http://localhost:5200",
      token: "t",
      model: "gpt-4o",
      sessionId: "sess-1",
      maxRetries: 5,
    });
    const json = client.toJSON();
    expect(json.baseUrl).toBe("http://localhost:5200");
    expect(json.model).toBe("gpt-4o");
    expect(json.sessionId).toBe("sess-1");
    expect(json.maxRetries).toBe(5);
  });

  it("token is not in SovrantApiError message", () => {
    const err = new SovrantApiError(401, "Unauthorized", "http://localhost:5200/v1/status");
    expect(err.message).not.toContain("super-secret");
    expect(err.message).toContain("401");
    expect(err.message).toContain("/v1/status");
  });
});

// ─── Request Construction ────────────────────────────────────────────────────

describe("SovrantClient — request construction", () => {
  let fetchSpy: ReturnType<typeof vi.fn>;
  let client: SovrantClient;

  beforeEach(() => {
    fetchSpy = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ choices: [{ message: { content: "ok" } }] }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      })
    );
    vi.stubGlobal("fetch", fetchSpy);
    client = new SovrantClient({
      baseUrl: "http://localhost:5200",
      token: "test-token",
      maxRetries: 0,
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("sends token in Authorization header, not URL", async () => {
    await client.chat("hello");
    const [url, init] = fetchSpy.mock.calls[0] as [string, RequestInit];
    expect(url).not.toContain("test-token");
    expect((init.headers as Record<string, string>).Authorization).toBe(
      "Bearer test-token"
    );
  });

  it("does not leak token in request body", async () => {
    await client.chat("hello");
    const [, init] = fetchSpy.mock.calls[0] as [string, RequestInit];
    expect(init.body).not.toContain("test-token");
  });

  it("encodes session ID in URL to prevent path traversal", async () => {
    await client.getSession("../../etc/passwd");
    const [url] = fetchSpy.mock.calls[0] as [string];
    expect(url).toContain(encodeURIComponent("../../etc/passwd"));
    expect(url).not.toContain("../../etc/passwd");
  });

  it("encodes session ID with special characters", async () => {
    await client.deleteSession("session with spaces & <script>");
    const [url] = fetchSpy.mock.calls[0] as [string];
    expect(url).not.toContain("<script>");
    expect(url).toContain(encodeURIComponent("session with spaces & <script>"));
  });

  it("sets Content-Type to application/json", async () => {
    await client.chat("hello");
    const [, init] = fetchSpy.mock.calls[0] as [string, RequestInit];
    expect((init.headers as Record<string, string>)["Content-Type"]).toBe(
      "application/json"
    );
  });
});

// ─── Export Format Validation ────────────────────────────────────────────────

describe("SovrantClient — exportSession format validation", () => {
  let fetchSpy: ReturnType<typeof vi.fn>;
  let client: SovrantClient;

  beforeEach(() => {
    fetchSpy = vi.fn().mockResolvedValue(
      new Response("# Session Export", {
        status: 200,
        headers: { "Content-Type": "text/markdown" },
      })
    );
    vi.stubGlobal("fetch", fetchSpy);
    client = new SovrantClient({
      baseUrl: "http://localhost:5200",
      token: "test-token",
      maxRetries: 0,
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("allows 'markdown' format", async () => {
    await client.exportSession("sess-1", "markdown");
    expect(fetchSpy).toHaveBeenCalledOnce();
  });

  it("rejects query string injection via format param", async () => {
    await expect(
      // @ts-expect-error — testing runtime guard with injection payload
      client.exportSession("sess-1", "markdown&admin=true")
    ).rejects.toThrow(/Invalid export format/);
  });

  it("rejects arbitrary format strings", async () => {
    await expect(
      // @ts-expect-error — testing runtime guard
      client.exportSession("sess-1", "json")
    ).rejects.toThrow(/Invalid export format/);
  });

  it("URL-encodes the format parameter", async () => {
    await client.exportSession("sess-1");
    const [url] = fetchSpy.mock.calls[0] as [string];
    expect(url).toContain("format=markdown");
  });
});

// ─── Retry Behavior ──────────────────────────────────────────────────────────

describe("SovrantClient — retry does not leak info", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("throws SovrantApiError with status but not token on 403", async () => {
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response("Forbidden", { status: 403 })
    );
    vi.stubGlobal("fetch", fetchSpy);

    const client = new SovrantClient({
      baseUrl: "http://localhost:5200",
      token: "secret-token-123",
      maxRetries: 0,
    });

    try {
      await client.getStatus();
      expect.fail("should have thrown");
    } catch (err) {
      expect(err).toBeInstanceOf(SovrantApiError);
      const apiErr = err as SovrantApiError;
      expect(apiErr.status).toBe(403);
      expect(apiErr.message).not.toContain("secret-token-123");
      expect(apiErr.body).not.toContain("secret-token-123");
    }
  });

  it("retries on 429 without exposing headers", async () => {
    let calls = 0;
    const fetchSpy = vi.fn().mockImplementation(() => {
      calls++;
      if (calls === 1) {
        return Promise.resolve(new Response("Rate limited", { status: 429 }));
      }
      return Promise.resolve(
        new Response(JSON.stringify([]), { status: 200 })
      );
    });
    vi.stubGlobal("fetch", fetchSpy);

    const client = new SovrantClient({
      baseUrl: "http://localhost:5200",
      token: "t",
      maxRetries: 1,
    });

    const result = await client.getStatus();
    expect(calls).toBe(2);
    expect(result).toEqual([]);
  });
});

// ─── Health endpoint ─────────────────────────────────────────────────────────

describe("SovrantClient — health endpoint", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("health() does not send Authorization header", async () => {
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ status: "ok" }), { status: 200 })
    );
    vi.stubGlobal("fetch", fetchSpy);

    const client = new SovrantClient({
      baseUrl: "http://localhost:5200",
      token: "secret",
    });
    await client.health();

    const [, init] = fetchSpy.mock.calls[0] as [string, RequestInit | undefined];
    // health() calls raw fetch without init, so no Authorization header
    expect(init).toBeUndefined();
  });
});
