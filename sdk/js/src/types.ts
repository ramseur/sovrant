/** Configuration for creating a SovrantClient. */
export interface SovrantClientOptions {
  /** Base URL of the Sovrant server (e.g. "http://localhost:5200"). */
  baseUrl: string;
  /** Bearer token for authentication against Sovrant.Server. */
  token: string;
  /** Default model to use for requests. */
  model?: string;
  /** Default session ID for persistent conversations. */
  sessionId?: string;
  /** Number of retry attempts on transient errors (default: 3). */
  maxRetries?: number;
  /** Request timeout in milliseconds (default: 120000). */
  timeoutMs?: number;
  /**
   * LLM provider API key for this client (multi-tenant use).
   * Sent as the `X-LLM-Api-Key` header — never in a URL or request body.
   * The server uses it for the LLM call and never logs or persists it.
   * Overrides the server's global `LLM_API_KEY` for every request this client makes.
   */
  llmApiKey?: string;
  /**
   * LLM provider base URL for this client (multi-tenant use).
   * Sent as the `X-LLM-Base-Url` header.
   * Allows each team to use a different LLM provider or endpoint.
   */
  llmBaseUrl?: string;
}

/** A message in the chat conversation. */
export interface ChatMessage {
  role: "user" | "assistant" | "system";
  content: string;
}

/** Request body for POST /v1/chat/completions. */
export interface ChatCompletionRequest {
  model?: string;
  messages: ChatMessage[];
  stream?: boolean;
  max_tokens?: number;
  session_id?: string;
}

/** Non-streaming response from POST /v1/chat/completions. */
export interface ChatCompletionResponse {
  id: string;
  object: string;
  model: string;
  choices: ResponseChoice[];
  usage?: UsageInfo;
}

export interface ResponseChoice {
  index: number;
  message: { role: string; content: string };
  finish_reason: string | null;
}

export interface UsageInfo {
  prompt_tokens: number;
  completion_tokens: number;
  total_tokens: number;
}

/** A streaming SSE chunk from POST /v1/chat/completions. */
export interface ChatCompletionChunk {
  id: string;
  object: string;
  model: string;
  choices: ChunkChoice[];
  usage?: UsageInfo;
  sovrant?: SovrantEvent;
}

export interface ChunkChoice {
  index: number;
  delta: { role?: string; content?: string };
  finish_reason: string | null;
}

/** Sovrant-specific extension event on a streaming chunk. */
export interface SovrantEvent {
  event: "tool_use" | "tool_result";
  tool_name: string;
  tool_use_id: string;
  is_error?: boolean;
}

/** Server status entry for a single provider. */
export interface ProviderStatus {
  name: string;
  healthy: boolean;
  latency_ms: number;
  request_count: number;
  error_count: number;
  score: string;
}

/** Server configuration (GET /v1/config). */
export interface ServerConfig {
  model: string;
  llm_base_url: string;
  permission_mode: string;
  pinned_provider?: string;
}

/** Webhook request body for POST /v1/webhook. */
export interface WebhookRequest {
  source: string;
  user_id: string;
  message: string;
  callback_url?: string;
  model?: string;
  thread_id?: string;
}

/** Webhook response from POST /v1/webhook. */
export interface WebhookResponse {
  success: boolean;
  source: string;
  user_id: string;
  thread_id?: string;
  session_id: string;
  text: string;
  tool_calls: WebhookToolCall[];
  errors: string[];
  input_tokens: number;
  output_tokens: number;
}

export interface WebhookToolCall {
  id: string;
  tool_name: string;
  is_error: boolean;
}

/** Per-call options for chat/stream/streamRaw methods. */
export interface ChatCallOptions {
  model?: string;
  sessionId?: string;
  llmApiKey?: string;
  llmBaseUrl?: string;
}

/** Response from GET /v1/models. */
export interface ModelsResponse {
  object: string;
  data: ModelInfo[];
}

export interface ModelInfo {
  id: string;
  object: string;
  owned_by?: string;
}

/** Response from GET /v1/sessions/:id. */
export interface SessionDetail {
  session_id: string;
  messages: SessionMessage[];
  total_input_tokens: number;
  total_output_tokens: number;
}

export interface SessionMessage {
  role: string;
  content: string;
  timestamp: string;
  input_tokens?: number;
  output_tokens?: number;
}

/** Response from GET /v1/sessions. */
export interface SessionListResponse {
  sessions: { id: string }[];
}

/** Response from GET /v1/usage. */
export interface UsageSummary {
  sessions: Record<string, { input_tokens: number; output_tokens: number }>;
  total_input_tokens: number;
  total_output_tokens: number;
}

/** Callbacks for streaming events. All callbacks may be sync or async. */
export interface StreamCallbacks {
  /** Called for each text chunk. */
  onText?: (text: string) => void | Promise<void>;
  /** Called when a tool invocation starts. */
  onToolUse?: (event: SovrantEvent) => void | Promise<void>;
  /** Called when a tool invocation completes. */
  onToolResult?: (event: SovrantEvent) => void | Promise<void>;
  /** Called when the turn is complete. */
  onComplete?: (response: { text: string; usage?: UsageInfo }) => void | Promise<void>;
  /** Called on errors. */
  onError?: (error: Error) => void | Promise<void>;
}
