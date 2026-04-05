import { parseSSEStream } from "./sse.js";
import type {
  ChatCompletionChunk,
  ChatCompletionRequest,
  ChatCompletionResponse,
  ChatMessage,
  ProviderStatus,
  ServerConfig,
  SovrantClientOptions,
  StreamCallbacks,
  UsageInfo,
  WebhookRequest,
  WebhookResponse,
} from "./types.js";

const DEFAULT_MAX_RETRIES = 3;
const RETRY_DELAYS = [1000, 2000, 4000];

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

  constructor(options: SovrantClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/, "");
    this.token = options.token;
    this.model = options.model;
    this.sessionId = options.sessionId;
    this.maxRetries = options.maxRetries ?? DEFAULT_MAX_RETRIES;
  }

  // ── Chat ──────────────────────────────────────────────────────────────

  /**
   * Send a message and get a complete (non-streaming) response.
   * Returns the assistant text and token usage.
   */
  async chat(
    message: string,
    options?: { model?: string; sessionId?: string }
  ): Promise<{ text: string; usage?: UsageInfo }> {
    const req = this.buildRequest(message, false, options);
    const res = await this.fetchWithRetry("/v1/chat/completions", {
      method: "POST",
      body: JSON.stringify(req),
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
    options?: { model?: string; sessionId?: string }
  ): Promise<void> {
    const req = this.buildRequest(message, true, options);
    const res = await this.fetchWithRetry("/v1/chat/completions", {
      method: "POST",
      body: JSON.stringify(req),
    });

    let fullText = "";
    let usage: UsageInfo | undefined;

    try {
      for await (const chunk of parseSSEStream(res)) {
        // Text content
        const content = chunk.choices?.[0]?.delta?.content;
        if (content) {
          fullText += content;
          callbacks.onText?.(content);
        }

        // Tool events (Sovrant extension)
        if (chunk.sovrant) {
          if (chunk.sovrant.event === "tool_use") {
            callbacks.onToolUse?.(chunk.sovrant);
          } else if (chunk.sovrant.event === "tool_result") {
            callbacks.onToolResult?.(chunk.sovrant);
          }
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
    options?: { model?: string; sessionId?: string }
  ): AsyncGenerator<ChatCompletionChunk> {
    const req = this.buildRequest(message, true, options);
    const res = await this.fetchWithRetry("/v1/chat/completions", {
      method: "POST",
      body: JSON.stringify(req),
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
  async getModels(): Promise<unknown> {
    const res = await this.fetchWithRetry("/v1/models");
    return res.json();
  }

  // ── Sessions ──────────────────────────────────────────────────────────

  /** List all session IDs. */
  async listSessions(): Promise<string[]> {
    const res = await this.fetchWithRetry("/v1/sessions");
    return (await res.json()) as string[];
  }

  /** Get session details including message history and token totals. */
  async getSession(
    sessionId: string
  ): Promise<Record<string, unknown>> {
    const res = await this.fetchWithRetry(`/v1/sessions/${encodeURIComponent(sessionId)}`);
    return (await res.json()) as Record<string, unknown>;
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
    const res = await this.fetchWithRetry(
      `/v1/sessions/${encodeURIComponent(sessionId)}/export?format=${format}`
    );
    return res.text();
  }

  // ── Usage ─────────────────────────────────────────────────────────────

  /** Get per-session token usage summary. */
  async getUsage(): Promise<Record<string, unknown>> {
    const res = await this.fetchWithRetry("/v1/usage");
    return (await res.json()) as Record<string, unknown>;
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
    options?: { model?: string; sessionId?: string }
  ): ChatCompletionRequest {
    const messages: ChatMessage[] = [{ role: "user", content: message }];
    return {
      model: options?.model ?? this.model,
      messages,
      stream,
      session_id: options?.sessionId ?? this.sessionId,
    };
  }

  private async fetchWithRetry(
    path: string,
    init?: RequestInit
  ): Promise<Response> {
    const url = `${this.baseUrl}${path}`;
    const headers: Record<string, string> = {
      Authorization: `Bearer ${this.token}`,
      "Content-Type": "application/json",
      ...(init?.headers as Record<string, string> | undefined),
    };

    let lastError: Error | undefined;

    for (let attempt = 0; attempt <= this.maxRetries; attempt++) {
      try {
        const res = await fetch(url, { ...init, headers });

        // Retry on 429 and 5xx.
        if (
          (res.status === 429 || res.status >= 500) &&
          attempt < this.maxRetries
        ) {
          await sleep(RETRY_DELAYS[attempt] ?? 4000);
          continue;
        }

        if (!res.ok) {
          const body = await res.text().catch(() => "");
          throw new SovrantApiError(res.status, body, url);
        }

        return res;
      } catch (err) {
        if (err instanceof SovrantApiError) throw err;
        lastError = err instanceof Error ? err : new Error(String(err));
        if (attempt < this.maxRetries) {
          await sleep(RETRY_DELAYS[attempt] ?? 4000);
        }
      }
    }

    throw lastError ?? new Error(`Request to ${url} failed after retries.`);
  }
}

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

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
