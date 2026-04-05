/** Configuration for creating a SovrantClient. */
export interface SovrantClientOptions {
  /** Base URL of the Sovrant server (e.g. "http://localhost:5200"). */
  baseUrl: string;
  /** Bearer token for authentication. */
  token: string;
  /** Default model to use for requests. */
  model?: string;
  /** Default session ID for persistent conversations. */
  sessionId?: string;
  /** Number of retry attempts on transient errors (default: 3). */
  maxRetries?: number;
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
  x_api_key?: string;
  x_base_url?: string;
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

/** Callbacks for streaming events. */
export interface StreamCallbacks {
  /** Called for each text chunk. */
  onText?: (text: string) => void;
  /** Called when a tool invocation starts. */
  onToolUse?: (event: SovrantEvent) => void;
  /** Called when a tool invocation completes. */
  onToolResult?: (event: SovrantEvent) => void;
  /** Called when the turn is complete. */
  onComplete?: (response: { text: string; usage?: UsageInfo }) => void;
  /** Called on errors. */
  onError?: (error: Error) => void;
}
