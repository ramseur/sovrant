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

/** Server status response from GET /v1/status. */
export interface StatusResponse {
  providers: ProviderStatus[];
  active_model: string;
  permission_mode: string;
  pinned_provider?: string;
  active_sessions: number;
  max_sessions: number;
  session_ttl_seconds: number;
}

/** Server status entry for a single provider. */
export interface ProviderStatus {
  name: string;
  healthy: boolean;
  latency_ms: number;
  request_count: number;
  error_count: number;
  error_rate: string;
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

/** Session config from GET /v1/sessions/:id/config. */
export interface SessionConfig {
  model: string;
  permission_mode: string;
  is_overridden: boolean;
}

/** Request body for PUT /v1/sessions/:id/config. */
export interface SessionConfigUpdate {
  model?: string;
  permission_mode?: string;
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

// ── Users ────────────────────────────────────────────────────────────────

/** User profile from GET /v1/users/me or GET /v1/users/:id. */
export interface UserProfile {
  user_id: string;
  username: string;
  email?: string;
  role: string;
  team?: string;
  status: string;
  created_at: string;
  updated_at: string;
}

/** Request body for POST /v1/users. */
export interface CreateUserRequest {
  username: string;
  email?: string;
  role?: string;
  team?: string;
}

/** Request body for PUT /v1/users/:id. */
export interface UpdateUserRequest {
  username?: string;
  email?: string;
  role?: string;
  team?: string;
  status?: string;
}

/** Filter parameters for GET /v1/users. */
export interface UserListFilter {
  status?: string;
  role?: string;
  team?: string;
  limit?: number;
  offset?: number;
}

/** API token metadata. */
export interface ApiToken {
  token_id: string;
  user_id: string;
  token_prefix: string;
  name?: string;
  scopes?: string;
  created_at: string;
  expires_at?: string;
  revoked_at?: string;
}

/** Request body for POST /v1/users/me/tokens. */
export interface IssueTokenRequest {
  name?: string;
  scopes?: string;
  expires_at?: string;
}

/** Response from POST /v1/users/me/tokens. */
export interface IssueTokenResponse {
  token: ApiToken;
  /** The plaintext bearer secret — returned once and never recoverable. */
  plaintext: string;
}

// ── Workspaces ───────────────────────────────────────────────────────────

/** Workspace record. */
export interface Workspace {
  workspace_id: string;
  name: string;
  slug: string;
  kind: string;
  owner_user_id: string;
  created_at: string;
  updated_at: string;
}

/** Request body for POST /v1/workspaces. */
export interface CreateWorkspaceRequest {
  name: string;
  slug: string;
}

/** Request body for PUT /v1/workspaces/:id. */
export interface UpdateWorkspaceRequest {
  name?: string;
  slug?: string;
}

/** Workspace member record. */
export interface WorkspaceMember {
  user_id: string;
  workspace_id: string;
  role: string;
  joined_at: string;
}

/** Request body for POST /v1/workspaces/:id/members. */
export interface AddWorkspaceMemberRequest {
  user_id: string;
  role?: string;
}

/** Workspace invite record. */
export interface WorkspaceInvite {
  invite_id: string;
  workspace_id: string;
  email: string;
  role: string;
  token: string;
  created_at: string;
  expires_at: string;
  accepted_at?: string;
}

/** Request body for POST /v1/workspaces/:id/invites. */
export interface CreateInviteRequest {
  email: string;
  role?: string;
}

/** Workspace memory entry. */
export interface WorkspaceMemoryEntry {
  memory_id: string;
  workspace_id: string;
  layer: string;
  content: string;
  confidence?: number;
  project_id?: string;
  created_at: string;
  updated_at: string;
}

/** Request body for POST /v1/workspaces/:id/memory. */
export interface SaveMemoryRequest {
  memory_id?: string;
  layer: string;
  content: string;
  confidence?: number;
  project_id?: string;
}

// ── Projects ─────────────────────────────────────────────────────────────

/** Project record. */
export interface Project {
  project_id: string;
  workspace_id: string;
  name: string;
  slug: string;
  description?: string;
  archived: boolean;
  created_at: string;
  updated_at: string;
}

/** Request body for POST /v1/workspaces/:wid/projects. */
export interface CreateProjectRequest {
  name: string;
  slug: string;
  description?: string;
}

/** Request body for PUT /v1/projects/:id. */
export interface UpdateProjectRequest {
  name?: string;
  slug?: string;
  description?: string;
}

/** Project member record. */
export interface ProjectMember {
  user_id: string;
  project_id: string;
  role: string;
  joined_at: string;
}

/** Request body for POST /v1/projects/:id/members. */
export interface AddProjectMemberRequest {
  user_id: string;
  role?: string;
}

// ── Teams ────────────────────────────────────────────────────────────────

/** Team record. */
export interface Team {
  id: string;
  workspace_id: string;
  project_id?: string;
  name: string;
  description?: string;
  origin: string;
  created_by: string;
  created_at: string;
}

/** Request body for POST /v1/teams. */
export interface CreateTeamRequest {
  name: string;
  description?: string;
  workspace_id?: string;
  project_id?: string;
  origin?: string;
  created_by?: string;
}

/** Team member record. */
export interface TeamMember {
  id: string;
  team_id: string;
  workspace_id: string;
  project_id?: string;
  name: string;
  role: string;
  template?: string;
  system_prompt: string;
  created_by: string;
}

/** Request body for POST /v1/teams/:id/members. */
export interface AddTeamMemberRequest {
  name: string;
  role?: string;
  template?: string;
  system_prompt?: string;
  created_by?: string;
}

/** Request body for POST /v1/teams/:id/runs. */
export interface TeamRunRequest {
  goal: string;
  user_id?: string;
  decompose?: boolean;
  lock_files?: boolean;
  quality_gate?: boolean;
  max_parallel?: number;
}

/** Response from POST /v1/teams/:id/runs. */
export interface TeamRunResponse {
  run_id: string;
  status: string;
  output: string;
  tokens_used: number;
}

/** Agent run record from GET /v1/runs/:id or GET /v1/runs. */
export interface AgentRun {
  run_id: string;
  workspace_id?: string;
  project_id?: string;
  user_id?: string;
  team_id?: string;
  kind: string;
  status: string;
  goal?: string;
  output?: string;
  tokens_used: number;
  created_at: string;
  completed_at?: string;
}

/** Filter parameters for GET /v1/runs. */
export interface AgentRunFilter {
  workspace_id?: string;
  user_id?: string;
  team_id?: string;
  kind?: string;
  status?: string;
  limit?: number;
}

// ── Missions ─────────────────────────────────────────────────────────────

/** Mission record. */
export interface Mission {
  id: string;
  goal: string;
  status: "planning" | "running" | "completed" | "failed";
  session_id?: string;
  workspace_id?: string;
  project_id?: string;
  owner_user_id?: string;
  created_at: string;
  updated_at: string;
}

/** Request body for POST /v1/missions. */
export interface CreateMissionRequest {
  goal: string;
  session_id?: string;
  workspace_id?: string;
  project_id?: string;
  owner_user_id?: string;
}

/** Mission event from GET /v1/missions/:id/events. */
export interface MissionEvent {
  event_id: string;
  mission_id: string;
  event_type: string;
  data: Record<string, unknown>;
  timestamp: string;
}

// ── Swarm ────────────────────────────────────────────────────────────────

/** Request body for POST /v1/swarm. */
export interface SwarmRunRequest {
  prompt: string;
  team?: string;
  dry_run?: boolean;
  budget?: number;
}

/** Swarm result from GET /v1/swarm/:id. */
export interface SwarmResult {
  swarm_id: string;
  status: string;
  combined_output: string;
  total_tokens_used: number;
  quality_gate?: Record<string, unknown>;
}

// ── Engine ───────────────────────────────────────────────────────────────

/** Runtime trace entry from GET /v1/engine/runs/:id/trace. */
export interface RuntimeTraceEntry {
  trace_id: string;
  runtime_run_id: string;
  step_type: string;
  step_data?: Record<string, unknown>;
  started_at: string;
  completed_at?: string;
}

// ── Evals ────────────────────────────────────────────────────────────────

/** Eval suite summary from GET /v1/evals. */
export interface EvalSuite {
  name: string;
  description?: string;
  eval_count: number;
  tags: string[];
}

/** Request body for POST /v1/evals/run. */
export interface EvalRunRequest {
  suite_name: string;
  tag?: string;
}

/** Response from POST /v1/evals/run. */
export interface EvalRunResponse {
  suite_name: string;
  started_at: string;
  duration_seconds: number;
  pass_rate: number;
  pass_at_1_rate: number;
  total_passed: number;
  total_failed: number;
  total_skipped: number;
  results: EvalResultDetail[];
}

export interface EvalResultDetail {
  eval_name: string;
  category: string;
  grader_type: string;
  passed: boolean;
  pass_at_1: boolean;
  pass_count: number;
  attempt_count: number;
  average_score?: number;
  duration_seconds: number;
  skipped: boolean;
}

// ── Artifacts ────────────────────────────────────────────────────────────

/** Artifact entry from GET /v1/artifacts. */
export interface ArtifactEntry {
  relative_path: string;
  size_bytes: number;
  content_type: string;
  last_modified: string;
  run_id?: string;
}

/** Scope parameters for artifact endpoints. */
export interface ArtifactScope {
  workspace_id?: string;
  project_id?: string;
  run_id?: string;
}

// ── Tool/Skill/Agent Registries ──────────────────────────────────────────

/** Tool definition from GET /v1/tools. */
export interface ToolDefinition {
  name: string;
  description?: string;
  parameters?: unknown;
}

/** Skill summary from GET /v1/skills. */
export interface SkillSummary {
  name: string;
  description: string;
  trigger?: string;
  agents?: string[];
  tools?: string[];
}

/** Skill detail from GET /v1/skills/:name (includes body). */
export interface SkillDetail extends SkillSummary {
  body: string;
}

/** Agent template summary from GET /v1/agents/templates. */
export interface AgentTemplateSummary {
  name: string;
  description: string;
  recommended_level: string;
  allowed_tools?: string[];
}

/** Agent template detail from GET /v1/agents/templates/:name. */
export interface AgentTemplateDetail {
  name: string;
  role: string;
  recommended_level: string;
  allowed_tools?: string[];
  system_prompt: string;
}
