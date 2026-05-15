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

// ── Auth (Phase 85 — Identity & Login Parity) ────────────────────────────

/** Body for POST /v1/auth/register. */
export interface RegisterRequest {
  email: string;
  password: string;
}

/** Body for POST /v1/auth/login. */
export interface LoginRequest {
  email: string;
  password: string;
}

/**
 * Result of a successful login or auto-approved register.
 * The token is an `svt_*` bearer the client should use for subsequent
 * Authorization headers.
 */
export interface AuthTokenResponse {
  token: string;
  user_id: string;
  role: string;
}

/**
 * Returned by POST /v1/auth/register. Either an immediate token (auto-approve
 * path) or a pending-approval acknowledgement (when admin approval is required).
 */
export type RegisterResponse =
  | AuthTokenResponse
  | {
      pending_approval: true;
      user_id: string;
      role: string;
      message: string;
    };

/** Type guard to discriminate the two RegisterResponse variants. */
export function isPendingApproval(
  response: RegisterResponse
): response is Extract<RegisterResponse, { pending_approval: true }> {
  return (response as { pending_approval?: boolean }).pending_approval === true;
}

/** Result of GET /v1/auth/me — lightweight identity probe. */
export interface AuthIdentity {
  user_id: string;
  role: string;
}

/** Body for POST /v1/auth/change-password. */
export interface ChangePasswordRequest {
  current_password: string;
  new_password: string;
}

/** Body for POST /v1/auth/use-reset-token. */
export interface UseResetTokenRequest {
  token: string;
  new_password: string;
}

/** Response shape for /v1/auth/registration/* endpoints. */
export interface RegistrationStatus {
  registration_open: boolean;
}

/** Response shape for /v1/auth/approval/* endpoints. */
export interface ApprovalStatus {
  approval_required: boolean;
}

/** Response from admin-issued POST /v1/users/{id}/reset-password. */
export interface IssueResetTokenResponse {
  /** The one-time plaintext reset token (24h TTL). Returned once. */
  reset_token: string;
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
  /** Phase 78 Path 2 — per-team run profile. */
  run_mode?: "sequential" | "parallel" | "swarm";
  max_concurrent?: number;
  file_locks_enabled?: boolean;
  quality_gate_enabled?: boolean;
  quality_gate_threshold?: number;
  decomposition_mode?: "off" | "roleAware" | "open";
}

/**
 * Request body for PUT /v1/teams/:id/profile (Phase 78 Path 2).
 * PATCH-style: any field omitted (or set to null) keeps its current value.
 *
 * The server parses enum values case-insensitively, so the documented
 * canonical values are listed in the union but any case variant is accepted
 * (the `& string` widening reflects this without losing autocomplete).
 */
export interface UpdateTeamProfileRequest {
  run_mode?: "sequential" | "parallel" | "swarm" | (string & {});
  max_concurrent?: number;
  file_locks_enabled?: boolean;
  quality_gate_enabled?: boolean;
  quality_gate_threshold?: number;
  decomposition_mode?: "off" | "roleAware" | "open" | (string & {});
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

/** Response from POST /v1/teams/:id/members. */
export interface AddTeamMemberResponse {
  member_id: string;
  name: string;
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

/**
 * Numeric SwarmStatus codes, matching the C# enum order on the wire.
 * `Results.Ok` returns enums as numbers because the server has no global
 * JsonStringEnumConverter registered.
 */
export const SwarmStatus = {
  Planning: 0,
  Executing: 1,
  QualityReview: 2,
  Completed: 3,
  Failed: 4,
  Cancelled: 5,
} as const;
export type SwarmStatusCode = (typeof SwarmStatus)[keyof typeof SwarmStatus];

/** Quality gate verdict from post-execution review. */
export interface QualityVerdict {
  score: number;
  verdict: string;
  feedback: string;
}

/**
 * Numeric SwarmTaskStatus codes, matching the C# enum order on the wire.
 * `Results.Ok` returns enums as numbers because the server has no global
 * JsonStringEnumConverter registered.
 */
export const SwarmTaskStatus = {
  Pending: 0,
  Blocked: 1,
  Running: 2,
  Completed: 3,
  Failed: 4,
  Cancelled: 5,
} as const;
export type SwarmTaskStatusCode = (typeof SwarmTaskStatus)[keyof typeof SwarmTaskStatus];

/** A single task node within a swarm plan. */
export interface SwarmTaskNode {
  id: string;
  description: string;
  dependencies: string[];
  filesToModify: string[];
  agentTemplate?: string;
  allowedTools?: string[];
  wave: number;
  status: SwarmTaskStatusCode;
  output?: string;
  error?: string;
  tokensUsed: number;
  retryCount: number;
}

/**
 * Swarm result from GET /v1/swarm/:id.
 * Wire format: camelCase (ASP.NET `Results.Ok` web defaults), numeric enum
 * for `status`.
 */
export interface SwarmResult {
  swarmId: string;
  status: SwarmStatusCode;
  tasks: SwarmTaskNode[];
  totalTokensUsed: number;
  duration: string;
  qualityGate?: QualityVerdict;
  combinedOutput: string;
}

/**
 * Discriminated union of swarm events as returned by GET /v1/swarm/:id/events.
 * Wire format: camelCase (Results.Ok). The discriminator is implicit — events
 * are distinguished by which fields they carry. The `event` field is
 * synthesised by the SDK only for SSE callbacks (see {@link SwarmSseEvent}).
 *
 * NOTE: The same logical events emitted via SSE on POST /v1/swarm use a
 * different (PascalCase) wire format because that endpoint serialises events
 * with no explicit naming policy. Use {@link SwarmSseEvent} for that path.
 */
export type SwarmEvent =
  | SwarmEventPlanCreated
  | SwarmEventTaskStarted
  | SwarmEventTaskCompleted
  | SwarmEventTaskFailed
  | SwarmEventFileConflict
  | SwarmEventBudgetExceeded
  | SwarmEventQualityGateStarted
  | SwarmEventQualityGateCompleted
  | SwarmEventSwarmCompleted
  | SwarmEventWaveCompleted
  | SwarmEventCoordinationReceived
  | SwarmEventCoordinationSent;

interface SwarmEventBase {
  swarmId: string;
  timestamp: string;
}

export interface SwarmEventPlanCreated extends SwarmEventBase {
  taskCount: number;
  waveCount: number;
}
export interface SwarmEventTaskStarted extends SwarmEventBase {
  taskId: string;
  agentName: string;
  taskDescription: string;
  wave: number;
}
export interface SwarmEventTaskCompleted extends SwarmEventBase {
  taskId: string;
  agentName: string;
  taskDescription: string;
  output: string;
  tokensUsed: number;
}
export interface SwarmEventTaskFailed extends SwarmEventBase {
  taskId: string;
  agentName: string;
  taskDescription: string;
  error: string;
  retryCount: number;
}
export interface SwarmEventFileConflict extends SwarmEventBase {
  taskId: string;
  agentName: string;
  filePath: string;
  heldByTaskId: string;
  heldByAgentName: string;
}
export interface SwarmEventBudgetExceeded extends SwarmEventBase {
  used: number;
  limit: number;
}
export type SwarmEventQualityGateStarted = SwarmEventBase;
export interface SwarmEventQualityGateCompleted extends SwarmEventBase {
  verdict: QualityVerdict;
}
export interface SwarmEventSwarmCompleted extends SwarmEventBase {
  finalStatus: SwarmStatusCode;
  totalTokens: number;
  durationSeconds: number;
}
export interface SwarmEventWaveCompleted extends SwarmEventBase {
  wave: number;
  completedTasks: number;
  failedTasks: number;
}
export interface SwarmEventCoordinationReceived extends SwarmEventBase {
  sourceGroupId: string;
  eventType: string;
  summary: string;
}
export interface SwarmEventCoordinationSent extends SwarmEventBase {
  targetGroupId: string;
  eventType: string;
  summary: string;
}

/**
 * SSE event payload from POST /v1/swarm. The `event` field is the SSE event
 * type (e.g. `"TaskStarted"`, `"plan"`, `"result"`, `"error"`); `data` is the
 * raw JSON parsed from the SSE `data:` line.
 *
 * The `data` shape for swarm event types is the same fields as the
 * corresponding {@link SwarmEvent} variant but with PascalCase property names,
 * because the server emits those events via a serializer with no naming
 * policy. For `event === "result"`, `data` is a {@link SwarmResult} also in
 * PascalCase. We keep `data` typed as `unknown` so callers can narrow it to
 * the shape they expect for their server build.
 */
export interface SwarmSseEvent {
  event: string;
  data: unknown;
}

/** Callbacks for {@link SovrantClient.runSwarm}. */
export interface SwarmStreamCallbacks {
  /** Called for every SSE event emitted by the server. */
  onEvent?: (event: SwarmSseEvent) => void | Promise<void>;
  /** Called once when the final `result` event arrives. */
  onResult?: (data: unknown) => void | Promise<void>;
  /** Called when the server emits an `error` event. */
  onServerError?: (data: unknown) => void | Promise<void>;
  /** Called on transport-level errors (network, parse, abort). */
  onError?: (error: Error) => void | Promise<void>;
  /** Called when the stream completes with `[DONE]`. */
  onComplete?: () => void | Promise<void>;
}

// ── Cost ─────────────────────────────────────────────────────────────────

/** Per-session cost row in {@link CostSummary.bySessions}. */
export interface SessionCostSummary {
  sessionId: string;
  estimatedUsd: number;
  inputTokens: number;
  outputTokens: number;
  turnCount: number;
}

/** Per-model cost row in {@link CostSummary.byModels}. */
export interface ModelCostSummary {
  model: string;
  estimatedUsd: number;
  inputTokens: number;
  outputTokens: number;
  turnCount: number;
}

/**
 * Cost summary returned by GET /v1/cost.
 * Wire format: camelCase (Results.Ok web defaults).
 */
export interface CostSummary {
  range: string;
  totalEstimatedUsd: number;
  totalActualUsd?: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  turnCount: number;
  sessionBudgetUsd?: number;
  projectBudgetUsd?: number;
  projectSpendUsd?: number;
  bySessions: SessionCostSummary[];
  byModels: ModelCostSummary[];
}

/** Returned by GET /v1/cost when the cost dashboard is disabled. */
export interface CostDisabled {
  enabled: false;
  message: string;
}

// ── Per-user / per-workspace / per-project usage ─────────────────────────

/** Per-model token usage row inside {@link UserUsage.by_model}. */
export interface UserUsageByModel {
  model: string;
  input_tokens: number;
  output_tokens: number;
  cost_usd: number;
}

/** GET /v1/users/:id/usage — wire format snake_case via JsonPropertyName. */
export interface UserUsage {
  user_id: string;
  total_input_tokens: number;
  total_output_tokens: number;
  total_cost_usd: number;
  session_count: number;
  by_model: UserUsageByModel[];
}

/**
 * GET /v1/workspaces/:id/usage — wire format camelCase
 * (no JsonPropertyName attributes; serialised by Results.Ok web defaults).
 */
export interface WorkspaceUsage {
  totalInputTokens: number;
  totalOutputTokens: number;
  totalCostUsd: number;
  sessionCount: number;
}

/** GET /v1/projects/:id/usage — wire format snake_case via JsonPropertyName. */
export interface ProjectUsage {
  total_input_tokens: number;
  total_output_tokens: number;
  total_cost_usd: number;
  session_count: number;
}

/** GET /v1/users/:id/audit — single audit event row. Wire format snake_case. */
export interface UserAuditEvent {
  id: string;
  timestamp: string;
  session_id?: string;
  workspace_id?: string;
  project_id?: string;
  phase: string;
  tool?: string;
  action: string;
  rule?: string;
  reason?: string;
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

/**
 * Historical eval report summary from GET /v1/evals/:suite/history.
 * Wire format: snake_case via JsonPropertyName.
 */
export interface EvalReportSummary {
  suite_name: string;
  started_at: string;
  duration_seconds: number;
  pass_rate: number;
  pass_at_1_rate: number;
  total_passed: number;
  total_failed: number;
  total_skipped: number;
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

// ── Command Center ────────────────────────────────────────────────────────

/** One active item row in the Command Center cockpit. */
export interface CommandCenterRow {
  kind: string;
  id: string;
  title: string;
  status: string;
  started_at: string;
  last_activity: string;
  owner_label?: string;
  preview?: string;
  cost_usd?: number;
  detail_route?: string;
  workspace_id?: string;
  project_id?: string;
}

/** Aggregated cockpit state from GET /v1/command-center/state. */
export interface CommandCenterState {
  generated_at: string;
  active_missions: number;
  active_team_runs: number;
  active_agent_runs: number;
  active_sessions: number;
  active_claws: number;
  rows: CommandCenterRow[];
}

// ── MCP Servers ───────────────────────────────────────────────────────────

/** One MCP server entry from GET /v1/mcp/servers. */
export interface McpServerEntry {
  name: string;
  connected: boolean;
}

// ── Knowledge Authoring ───────────────────────────────────────────────────

/** Response from POST /v1/knowledge/:kind/:slug. */
export interface KnowledgeSaveResponse {
  slug: string;
  path: string;
}
