import { parseSSEStream } from "./sse.js";
import type {
  ChatCallOptions,
  ChatCompletionChunk,
  ChatCompletionRequest,
  ChatCompletionResponse,
  ChatMessage,
  ModelsResponse,
  ProviderStatus,
  ServerConfig,
  SessionDetail,
  SessionListResponse,
  SovrantClientOptions,
  StreamCallbacks,
  UsageInfo,
  UsageSummary,
  WebhookRequest,
  WebhookResponse,
} from "./types.js";

const DEFAULT_MAX_RETRIES = 3;
const DEFAULT_TIMEOUT_MS = 120_000;
const BASE_RETRY_DELAYS = [1000, 2000, 4000];
const ALLOWED_PROTOCOLS = ["http:", "https:"];
const ALLOWED_EXPORT_FORMATS = ["markdown"];

/**
 * Typed client for the Sovrant server API.
 *
 * Handles authentication, SSE streaming, session management, retry on
 * transient errors, and tool event dispatch.
 *
 * @example
 * ```ts
 * const client = new SovrantClient({
 *   baseUrl: "http://localhost:5200",
 *   token: "your-secret-token",
 *   model: "gpt-4o-mini",
 * });
 *
 * // Non-streaming
 * const response = await client.chat("Hello!");
 * console.log(response.text);
 *
 * // Streaming
 * await client.stream("Explain async/await", {
 *   onText: (chunk) => process.stdout.write(chunk),
 *   onComplete: ({ usage }) => console.log(usage),
 * });
 * ```
 */
export class SovrantClient {
  private readonly baseUrl: string;
  private readonly token: string;
  private readonly model?: string;
  private readonly sessionId?: string;
  private readonly maxRetries: number;
  private readonly timeoutMs: number;
  private readonly llmApiKey?: string;
  private readonly llmBaseUrl?: string;

  constructor(options: SovrantClientOptions) {
    const trimmed = options.baseUrl.replace(/\/+$/, "");
    // Validate URL protocol to prevent javascript:, file:, data: etc.
    try {
      const parsed = new URL(trimmed);
      if (!ALLOWED_PROTOCOLS.includes(parsed.protocol)) {
        throw new Error(
          `Invalid baseUrl protocol "${parsed.protocol}". Only http: and https: are allowed.`
        );
      }
    } catch (err) {
      if (err instanceof TypeError) {
        throw new Error(`Invalid baseUrl: "${trimmed}" is not a valid URL.`);
      }
      throw err;
    }

    if (!options.token || typeof options.token !== "string") {
      throw new Error("A non-empty token string is required.");
    }

    this.baseUrl = trimmed;
    this.token = options.token;
    this.model = options.model;
    this.sessionId = options.sessionId;
    this.maxRetries = options.maxRetries ?? DEFAULT_MAX_RETRIES;
    this.timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
    this.llmApiKey = options.llmApiKey;
    this.llmBaseUrl = options.llmBaseUrl;
  }

  /** Prevent credential leakage via JSON.stringify / console.log. */
  toJSON(): Record<string, unknown> {
    return {
      baseUrl: this.baseUrl,
      model: this.model,
      sessionId: this.sessionId,
      maxRetries: this.maxRetries,
      timeoutMs: this.timeoutMs,
      token: "[REDACTED]",
      llmApiKey: this.llmApiKey !== undefined ? "[REDACTED]" : undefined,
      llmBaseUrl: this.llmBaseUrl,
    };
  }

  // ── Chat ──────────────────────────────────────────────────────────────

  /**
   * Send a message and get a complete (non-streaming) response.
   * Returns the assistant text and token usage.
   */
  async chat(
    message: string,
    options?: ChatCallOptions
  ): Promise<{ text: string; usage?: UsageInfo }> {
    const req = this.buildRequest(message, false, options);
    const res = await this.fetchWithRetry("/v1/chat/completions", {
      method: "POST",
      body: JSON.stringify(req),
      headers: this.buildLlmHeaders(options),
    });
    const data = (await res.json()) as ChatCompletionResponse;
    return {
      text: data.choices?.[0]?.message?.content ?? "",
      usage: data.usage,
    };
  }

  /**
   * Send a message and stream the response via SSE.
   * Invokes callbacks for text chunks, tool events, and completion.
   */
  async stream(
    message: string,
    callbacks: StreamCallbacks,
    options?: ChatCallOptions
  ): Promise<void> {
    const req = this.buildRequest(message, true, options);
    const res = await this.fetchWithRetry("/v1/chat/completions", {
      method: "POST",
      body: JSON.stringify(req),
      headers: this.buildLlmHeaders(options),
    });

    let fullText = "";
    let usage: UsageInfo | undefined;

    try {
      for await (const chunk of parseSSEStream(res)) {
        // Text content
        const content = chunk.choices?.[0]?.delta?.content;
        if (content) {
          fullText += content;
          try { callbacks.onText?.(content); } catch { /* callback error — non-fatal */ }
        }

        // Tool events (Sovrant extension)
        if (chunk.sovrant) {
          try {
            if (chunk.sovrant.event === "tool_use") {
              callbacks.onToolUse?.(chunk.sovrant);
            } else if (chunk.sovrant.event === "tool_result") {
              callbacks.onToolResult?.(chunk.sovrant);
            }
          } catch { /* callback error — non-fatal */ }
        }

        // Usage info (final chunk)
        if (chunk.usage) {
          usage = chunk.usage;
        }
      }

      callbacks.onComplete?.({ text: fullText, usage });
    } catch (err) {
      callbacks.onError?.(
        err instanceof Error ? err : new Error(String(err))
      );
    }
  }

  /**
   * Returns an async iterable of raw SSE chunks for advanced use cases.
   */
  async *streamRaw(
    message: string,
    options?: ChatCallOptions
  ): AsyncGenerator<ChatCompletionChunk> {
    const req = this.buildRequest(message, true, options);
    const res = await this.fetchWithRetry("/v1/chat/completions", {
      method: "POST",
      body: JSON.stringify(req),
      headers: this.buildLlmHeaders(options),
    });
    yield* parseSSEStream(res);
  }

  // ── Webhook ───────────────────────────────────────────────────────────

  /** Send a message via the webhook endpoint. */
  async webhook(request: WebhookRequest): Promise<WebhookResponse> {
    const res = await this.fetchWithRetry("/v1/webhook", {
      method: "POST",
      body: JSON.stringify(request),
    });
    return (await res.json()) as WebhookResponse;
  }

  // ── Config ────────────────────────────────────────────────────────────

  /** Get the current server configuration. */
  async getConfig(): Promise<ServerConfig> {
    const res = await this.fetchWithRetry("/v1/config");
    return (await res.json()) as ServerConfig;
  }

  /** Update the server configuration. */
  async updateConfig(
    updates: Partial<ServerConfig>
  ): Promise<ServerConfig> {
    const res = await this.fetchWithRetry("/v1/config", {
      method: "PUT",
      body: JSON.stringify(updates),
    });
    return (await res.json()) as ServerConfig;
  }

  // ── Status ────────────────────────────────────────────────────────────

  /** Get provider health and routing status. */
  async getStatus(): Promise<ProviderStatus[]> {
    const res = await this.fetchWithRetry("/v1/status");
    return (await res.json()) as ProviderStatus[];
  }

  /** Get available models. */
  async getModels(): Promise<ModelsResponse> {
    const res = await this.fetchWithRetry("/v1/models");
    return (await res.json()) as ModelsResponse;
  }

  // ── Sessions ──────────────────────────────────────────────────────────

  /** List all session IDs. */
  async listSessions(): Promise<SessionListResponse> {
    const res = await this.fetchWithRetry("/v1/sessions");
    return (await res.json()) as SessionListResponse;
  }

  /** Get session details including message history and token totals. */
  async getSession(sessionId: string): Promise<SessionDetail> {
    const res = await this.fetchWithRetry(`/v1/sessions/${encodeURIComponent(sessionId)}`);
    return (await res.json()) as SessionDetail;
  }

  /** Delete a session. */
  async deleteSession(sessionId: string): Promise<void> {
    await this.fetchWithRetry(
      `/v1/sessions/${encodeURIComponent(sessionId)}`,
      { method: "DELETE" }
    );
  }

  /** Export a session as markdown. */
  async exportSession(
    sessionId: string,
    format: "markdown" = "markdown"
  ): Promise<string> {
    if (!ALLOWED_EXPORT_FORMATS.includes(format)) {
      throw new Error(
        `Invalid export format "${format}". Allowed: ${ALLOWED_EXPORT_FORMATS.join(", ")}`
      );
    }
    const res = await this.fetchWithRetry(
      `/v1/sessions/${encodeURIComponent(sessionId)}/export?format=${encodeURIComponent(format)}`
    );
    return res.text();
  }

  // ── Usage ─────────────────────────────────────────────────────────────

  /** Get per-session token usage summary. */
  async getUsage(): Promise<UsageSummary> {
    const res = await this.fetchWithRetry("/v1/usage");
    return (await res.json()) as UsageSummary;
  }

  // ── Health ────────────────────────────────────────────────────────────

  /** Check server health (unauthenticated). */
  async health(): Promise<{ status: string }> {
    const res = await fetch(`${this.baseUrl}/health`);
    return (await res.json()) as { status: string };
  }

  // ── Internal ──────────────────────────────────────────────────────────

  private buildRequest(
    message: string,
    stream: boolean,
    options?: ChatCallOptions
  ): ChatCompletionRequest {
    const messages: ChatMessage[] = [{ role: "user", content: message }];
    return {
      model: options?.model ?? this.model,
      messages,
      stream,
      session_id: options?.sessionId ?? this.sessionId,
    };
  }

  /** Resolve per-request LLM credential headers (per-call options override constructor defaults). */
  private buildLlmHeaders(
    options?: ChatCallOptions
  ): Record<string, string> {
    const headers: Record<string, string> = {};
    const llmApiKey = options?.llmApiKey ?? this.llmApiKey;
    const llmBaseUrl = options?.llmBaseUrl ?? this.llmBaseUrl;
    if (llmApiKey !== undefined) headers["X-LLM-Api-Key"] = llmApiKey;
    if (llmBaseUrl !== undefined) headers["X-LLM-Base-Url"] = llmBaseUrl;
    return headers;
  }

  private async fetchWithRetry(
    path: string,
    init?: RequestInit
  ): Promise<Response> {
    const url = `${this.baseUrl}${path}`;
    const extraHeaders = init?.headers as Record<string, string> | undefined;
    const headers: Record<string, string> = {
      Authorization: `Bearer ${this.token}`,
      "Content-Type": "application/json",
      ...extraHeaders,
    };

    let lastError: Error | undefined;

    for (let attempt = 0; attempt <= this.maxRetries; attempt++) {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), this.timeoutMs);

      try {
        const res = await fetch(url, {
          ...init,
          headers,
          signal: controller.signal,
        });

        clearTimeout(timeoutId);

        // Retry on 429 and 5xx.
        if (
          (res.status === 429 || res.status >= 500) &&
          attempt < this.maxRetries
        ) {
          await sleep(retryDelay(attempt));
          continue;
        }

        if (!res.ok) {
          const body = await res.text().catch(() => "");
          if (res.status === 429) throw new SovrantRateLimitError(body, url);
          if (res.status === 401 || res.status === 403) throw new SovrantAuthError(res.status, body, url);
          throw new SovrantApiError(res.status, body, url);
        }

        return res;
      } catch (err) {
        clearTimeout(timeoutId);

        if (err instanceof SovrantApiError) throw err;

        // AbortController fires on timeout
        if (err instanceof DOMException && err.name === "AbortError") {
          lastError = new SovrantTimeoutError(url, this.timeoutMs);
          if (attempt < this.maxRetries) {
            await sleep(retryDelay(attempt));
            continue;
          }
          break;
        }

        lastError = err instanceof Error ? err : new Error(String(err));
        if (attempt < this.maxRetries) {
          await sleep(retryDelay(attempt));
        }
      }
    }

    throw lastError ?? new Error(`Request to ${url} failed after retries.`);
  }
}

// ── Errors ──────────────────────────────────────────────────────────────

/** Error thrown when the Sovrant API returns a non-OK response. */
export class SovrantApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly body: string,
    public readonly url: string
  ) {
    super(`Sovrant API error ${status} from ${url}: ${body}`);
    this.name = "SovrantApiError";
  }
}

/** Error thrown on 401/403 authentication failures. */
export class SovrantAuthError extends SovrantApiError {
  constructor(status: number, body: string, url: string) {
    super(status, body, url);
    this.name = "SovrantAuthError";
  }
}

/** Error thrown on 429 rate limit responses. */
export class SovrantRateLimitError extends SovrantApiError {
  constructor(body: string, url: string) {
    super(429, body, url);
    this.name = "SovrantRateLimitError";
  }
}

/** Error thrown when a request times out. */
export class SovrantTimeoutError extends Error {
  constructor(
    public readonly url: string,
    public readonly timeoutMs: number
  ) {
    super(`Request to ${url} timed out after ${timeoutMs}ms`);
    this.name = "SovrantTimeoutError";
  }
}

// ── Helpers ─────────────────────────────────────────────────────────────

/** Exponential backoff with jitter to prevent thundering herd. */
function retryDelay(attempt: number): number {
  const base = BASE_RETRY_DELAYS[attempt] ?? 4000;
  // Add ±25% jitter
  const jitter = base * 0.25 * (Math.random() * 2 - 1);
  return Math.max(0, Math.round(base + jitter));
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
