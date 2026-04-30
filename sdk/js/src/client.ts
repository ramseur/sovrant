import { parseSSEStream } from "./sse.js";
import type {
  AddProjectMemberRequest,
  AddTeamMemberRequest,
  AddTeamMemberResponse,
  AddWorkspaceMemberRequest,
  AgentRun,
  AgentRunFilter,
  AgentTemplateDetail,
  AgentTemplateSummary,
  ArtifactEntry,
  ArtifactScope,
  ApiToken,
  ChatCallOptions,
  ChatCompletionChunk,
  ChatCompletionRequest,
  ChatCompletionResponse,
  ChatMessage,
  CostDisabled,
  CostSummary,
  CreateInviteRequest,
  CreateMissionRequest,
  CreateProjectRequest,
  CreateTeamRequest,
  CreateUserRequest,
  CreateWorkspaceRequest,
  EvalReportSummary,
  EvalRunRequest,
  EvalRunResponse,
  EvalSuite,
  IssueTokenRequest,
  IssueTokenResponse,
  Mission,
  MissionEvent,
  ModelsResponse,
  Project,
  ProjectMember,
  ProjectUsage,
  RuntimeTraceEntry,
  SaveMemoryRequest,
  ServerConfig,
  SessionConfig,
  SessionConfigUpdate,
  SessionDetail,
  SessionListResponse,
  SkillDetail,
  SkillSummary,
  SovrantClientOptions,
  StatusResponse,
  StreamCallbacks,
  SwarmEvent,
  SwarmResult,
  SwarmRunRequest,
  SwarmSseEvent,
  SwarmStreamCallbacks,
  Team,
  TeamMember,
  TeamRunRequest,
  TeamRunResponse,
  ToolDefinition,
  UpdateProjectRequest,
  UpdateTeamProfileRequest,
  UpdateUserRequest,
  UpdateWorkspaceRequest,
  UsageInfo,
  UsageSummary,
  UserAuditEvent,
  UserListFilter,
  UserProfile,
  UserUsage,
  WebhookRequest,
  WebhookResponse,
  Workspace,
  WorkspaceInvite,
  WorkspaceMember,
  WorkspaceMemoryEntry,
  WorkspaceUsage,
} from "./types.js";

const DEFAULT_MAX_RETRIES = 3;
const DEFAULT_TIMEOUT_MS = 120_000;
const BASE_RETRY_DELAYS = [1000, 2000, 4000];
const ALLOWED_PROTOCOLS = ["http:", "https:"];
const ALLOWED_EXPORT_FORMATS = ["markdown", "json"];

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

  /** Get server status including provider health, session pool, and routing info. */
  async getStatus(): Promise<StatusResponse> {
    const res = await this.fetchWithRetry("/v1/status");
    return (await res.json()) as StatusResponse;
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

  /** Get session-level config overrides (model, permission mode). */
  async getSessionConfig(sessionId: string): Promise<SessionConfig> {
    const res = await this.fetchWithRetry(
      `/v1/sessions/${encodeURIComponent(sessionId)}/config`
    );
    return (await res.json()) as SessionConfig;
  }

  /** Update session-level config overrides. */
  async updateSessionConfig(
    sessionId: string,
    update: SessionConfigUpdate
  ): Promise<void> {
    await this.fetchWithRetry(
      `/v1/sessions/${encodeURIComponent(sessionId)}/config`,
      { method: "PUT", body: JSON.stringify(update) }
    );
  }

  /** Export a session as markdown or JSON. */
  async exportSession(
    sessionId: string,
    format: "markdown" | "json" = "markdown"
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

  // ── Cost ──────────────────────────────────────────────────────────────

  /**
   * Get the cost dashboard summary from GET /v1/cost.
   *
   * Returns a {@link CostSummary} with token totals, estimated USD spend,
   * per-session and per-model breakdowns. If the cost dashboard is disabled
   * server-side, returns a {@link CostDisabled} object instead — discriminate
   * via the `enabled` field.
   */
  async getCost(options?: {
    range?: "daily" | "weekly" | "monthly" | "all";
  }): Promise<CostSummary | CostDisabled> {
    const params = new URLSearchParams();
    if (options?.range) params.set("range", options.range);
    const qs = params.toString();
    const res = await this.fetchWithRetry(`/v1/cost${qs ? `?${qs}` : ""}`);
    return (await res.json()) as CostSummary | CostDisabled;
  }

  // ── Health ────────────────────────────────────────────────────────────

  /** Check server health (unauthenticated). */
  async health(): Promise<{ status: string }> {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this.timeoutMs);
    try {
      const res = await fetch(`${this.baseUrl}/health`, {
        signal: controller.signal,
      });
      if (!res.ok) {
        const body = await res.text().catch(() => "");
        throw new SovrantApiError(res.status, body, `${this.baseUrl}/health`);
      }
      return (await res.json()) as { status: string };
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") {
        throw new SovrantTimeoutError(`${this.baseUrl}/health`, this.timeoutMs);
      }
      throw err;
    } finally {
      clearTimeout(timeoutId);
    }
  }

  // ── Users (Me) ────────────────────────────────────────────────────────

  /** Get the authenticated user's profile. */
  async getMe(): Promise<UserProfile> {
    const res = await this.fetchWithRetry("/v1/users/me");
    return (await res.json()) as UserProfile;
  }

  /** Issue a new API token for the authenticated user. */
  async issueToken(request?: IssueTokenRequest): Promise<IssueTokenResponse> {
    const res = await this.fetchWithRetry("/v1/users/me/tokens", {
      method: "POST",
      body: JSON.stringify(request ?? {}),
    });
    return (await res.json()) as IssueTokenResponse;
  }

  /** List all API tokens for the authenticated user. */
  async listTokens(): Promise<{ tokens: ApiToken[]; count: number }> {
    const res = await this.fetchWithRetry("/v1/users/me/tokens");
    return (await res.json()) as { tokens: ApiToken[]; count: number };
  }

  /** Revoke an API token by ID. */
  async revokeToken(tokenId: string): Promise<void> {
    await this.fetchWithRetry(
      `/v1/users/me/tokens/${encodeURIComponent(tokenId)}`,
      { method: "DELETE" }
    );
  }

  // ── Users (Admin) ─────────────────────────────────────────────────────

  /** Create a new user (admin only). */
  async createUser(request: CreateUserRequest): Promise<UserProfile> {
    const res = await this.fetchWithRetry("/v1/users", {
      method: "POST",
      body: JSON.stringify(request),
    });
    return (await res.json()) as UserProfile;
  }

  /** List users with optional filters (admin only). */
  async listUsers(filter?: UserListFilter): Promise<{ users: UserProfile[]; count: number; total: number }> {
    const params = new URLSearchParams();
    if (filter?.status) params.set("status", filter.status);
    if (filter?.role) params.set("role", filter.role);
    if (filter?.team) params.set("team", filter.team);
    if (filter?.limit !== undefined) params.set("limit", String(filter.limit));
    if (filter?.offset !== undefined) params.set("offset", String(filter.offset));
    const qs = params.toString();
    const res = await this.fetchWithRetry(`/v1/users${qs ? `?${qs}` : ""}`);
    return (await res.json()) as { users: UserProfile[]; count: number; total: number };
  }

  /** Get a user by ID (admin or self). */
  async getUser(userId: string): Promise<UserProfile> {
    const res = await this.fetchWithRetry(`/v1/users/${encodeURIComponent(userId)}`);
    return (await res.json()) as UserProfile;
  }

  /** Update a user (admin only). */
  async updateUser(userId: string, request: UpdateUserRequest): Promise<UserProfile> {
    const res = await this.fetchWithRetry(`/v1/users/${encodeURIComponent(userId)}`, {
      method: "PUT",
      body: JSON.stringify(request),
    });
    return (await res.json()) as UserProfile;
  }

  /** Deactivate a user (admin only). Soft-delete — sets status to inactive. */
  async deactivateUser(userId: string): Promise<void> {
    await this.fetchWithRetry(`/v1/users/${encodeURIComponent(userId)}`, {
      method: "DELETE",
    });
  }

  /** Reactivate a deactivated user (admin only). */
  async reactivateUser(userId: string): Promise<void> {
    await this.fetchWithRetry(`/v1/users/${encodeURIComponent(userId)}/reactivate`, {
      method: "POST",
    });
  }

  /** List session IDs owned by a user (admin or self). */
  async listUserSessions(
    userId: string,
    options?: { limit?: number; offset?: number }
  ): Promise<{ sessions: string[]; count: number }> {
    const params = new URLSearchParams();
    if (options?.limit !== undefined) params.set("limit", String(options.limit));
    if (options?.offset !== undefined) params.set("offset", String(options.offset));
    const qs = params.toString();
    const res = await this.fetchWithRetry(
      `/v1/users/${encodeURIComponent(userId)}/sessions${qs ? `?${qs}` : ""}`
    );
    return (await res.json()) as { sessions: string[]; count: number };
  }

  /** Get usage stats for a user (admin or self). */
  async getUserUsage(
    userId: string,
    options?: { model?: string; from?: string; to?: string }
  ): Promise<UserUsage> {
    const params = new URLSearchParams();
    if (options?.model) params.set("model", options.model);
    if (options?.from) params.set("from", options.from);
    if (options?.to) params.set("to", options.to);
    const qs = params.toString();
    const res = await this.fetchWithRetry(
      `/v1/users/${encodeURIComponent(userId)}/usage${qs ? `?${qs}` : ""}`
    );
    return (await res.json()) as UserUsage;
  }

  /** Get audit log for a user (admin or self). */
  async getUserAudit(
    userId: string,
    options?: { limit?: number }
  ): Promise<{ events: UserAuditEvent[]; count: number }> {
    const params = new URLSearchParams();
    if (options?.limit !== undefined) params.set("limit", String(options.limit));
    const qs = params.toString();
    const res = await this.fetchWithRetry(
      `/v1/users/${encodeURIComponent(userId)}/audit${qs ? `?${qs}` : ""}`
    );
    return (await res.json()) as { events: UserAuditEvent[]; count: number };
  }

  // ── Workspaces ────────────────────────────────────────────────────────

  /** List workspaces the authenticated user belongs to. */
  async listWorkspaces(): Promise<{ workspaces: Workspace[] }> {
    const res = await this.fetchWithRetry("/v1/workspaces");
    return (await res.json()) as { workspaces: Workspace[] };
  }

  /** Create a new team workspace. */
  async createWorkspace(request: CreateWorkspaceRequest): Promise<Workspace> {
    const res = await this.fetchWithRetry("/v1/workspaces", {
      method: "POST",
      body: JSON.stringify(request),
    });
    return (await res.json()) as Workspace;
  }

  /** Get a workspace by ID. */
  async getWorkspace(workspaceId: string): Promise<Workspace> {
    const res = await this.fetchWithRetry(`/v1/workspaces/${encodeURIComponent(workspaceId)}`);
    return (await res.json()) as Workspace;
  }

  /** Update a workspace. */
  async updateWorkspace(workspaceId: string, request: UpdateWorkspaceRequest): Promise<Workspace> {
    const res = await this.fetchWithRetry(`/v1/workspaces/${encodeURIComponent(workspaceId)}`, {
      method: "PUT",
      body: JSON.stringify(request),
    });
    return (await res.json()) as Workspace;
  }

  /** Delete a workspace. Personal workspaces cannot be deleted. */
  async deleteWorkspace(workspaceId: string): Promise<void> {
    await this.fetchWithRetry(`/v1/workspaces/${encodeURIComponent(workspaceId)}`, {
      method: "DELETE",
    });
  }

  /** List members of a workspace. */
  async listWorkspaceMembers(workspaceId: string): Promise<{ members: WorkspaceMember[] }> {
    const res = await this.fetchWithRetry(`/v1/workspaces/${encodeURIComponent(workspaceId)}/members`);
    return (await res.json()) as { members: WorkspaceMember[] };
  }

  /** Add a member to a workspace. */
  async addWorkspaceMember(workspaceId: string, request: AddWorkspaceMemberRequest): Promise<void> {
    await this.fetchWithRetry(`/v1/workspaces/${encodeURIComponent(workspaceId)}/members`, {
      method: "POST",
      body: JSON.stringify(request),
    });
  }

  /** Remove a member from a workspace. */
  async removeWorkspaceMember(workspaceId: string, userId: string): Promise<void> {
    await this.fetchWithRetry(
      `/v1/workspaces/${encodeURIComponent(workspaceId)}/members/${encodeURIComponent(userId)}`,
      { method: "DELETE" }
    );
  }

  /** Create a workspace invite. */
  async createWorkspaceInvite(workspaceId: string, request: CreateInviteRequest): Promise<WorkspaceInvite> {
    const res = await this.fetchWithRetry(`/v1/workspaces/${encodeURIComponent(workspaceId)}/invites`, {
      method: "POST",
      body: JSON.stringify(request),
    });
    return (await res.json()) as WorkspaceInvite;
  }

  /** Delete a workspace invite. */
  async deleteWorkspaceInvite(workspaceId: string, inviteId: string): Promise<void> {
    await this.fetchWithRetry(
      `/v1/workspaces/${encodeURIComponent(workspaceId)}/invites/${encodeURIComponent(inviteId)}`,
      { method: "DELETE" }
    );
  }

  /** Accept a workspace invite by token. */
  async acceptWorkspaceInvite(token: string): Promise<void> {
    await this.fetchWithRetry("/v1/workspaces/invites/accept", {
      method: "POST",
      body: JSON.stringify({ token }),
    });
  }

  /** Get workspace configuration. */
  async getWorkspaceConfig(workspaceId: string): Promise<{ config: Record<string, string> }> {
    const res = await this.fetchWithRetry(`/v1/workspaces/${encodeURIComponent(workspaceId)}/config`);
    return (await res.json()) as { config: Record<string, string> };
  }

  /** Update workspace configuration. */
  async updateWorkspaceConfig(workspaceId: string, values: Record<string, string>): Promise<void> {
    await this.fetchWithRetry(`/v1/workspaces/${encodeURIComponent(workspaceId)}/config`, {
      method: "PUT",
      body: JSON.stringify(values),
    });
  }

  /** Get workspace usage stats. */
  async getWorkspaceUsage(workspaceId: string): Promise<WorkspaceUsage> {
    const res = await this.fetchWithRetry(`/v1/workspaces/${encodeURIComponent(workspaceId)}/usage`);
    return (await res.json()) as WorkspaceUsage;
  }

  /** List workspace memory entries. */
  async listWorkspaceMemory(
    workspaceId: string,
    layer?: string
  ): Promise<{ memory: WorkspaceMemoryEntry[] }> {
    const qs = layer ? `?layer=${encodeURIComponent(layer)}` : "";
    const res = await this.fetchWithRetry(`/v1/workspaces/${encodeURIComponent(workspaceId)}/memory${qs}`);
    return (await res.json()) as { memory: WorkspaceMemoryEntry[] };
  }

  /** Save a workspace memory entry. */
  async saveWorkspaceMemory(workspaceId: string, request: SaveMemoryRequest): Promise<WorkspaceMemoryEntry> {
    const res = await this.fetchWithRetry(`/v1/workspaces/${encodeURIComponent(workspaceId)}/memory`, {
      method: "POST",
      body: JSON.stringify(request),
    });
    return (await res.json()) as WorkspaceMemoryEntry;
  }

  /** Delete a workspace memory entry. */
  async deleteWorkspaceMemory(workspaceId: string, memoryId: string): Promise<void> {
    await this.fetchWithRetry(
      `/v1/workspaces/${encodeURIComponent(workspaceId)}/memory/${encodeURIComponent(memoryId)}`,
      { method: "DELETE" }
    );
  }

  // ── Projects ──────────────────────────────────────────────────────────

  /** List projects in a workspace. */
  async listProjects(
    workspaceId: string,
    options?: { includeArchived?: boolean }
  ): Promise<{ projects: Project[] }> {
    const qs = options?.includeArchived ? "?includeArchived=true" : "";
    const res = await this.fetchWithRetry(
      `/v1/workspaces/${encodeURIComponent(workspaceId)}/projects${qs}`
    );
    return (await res.json()) as { projects: Project[] };
  }

  /** Create a project in a workspace. */
  async createProject(workspaceId: string, request: CreateProjectRequest): Promise<Project> {
    const res = await this.fetchWithRetry(
      `/v1/workspaces/${encodeURIComponent(workspaceId)}/projects`,
      { method: "POST", body: JSON.stringify(request) }
    );
    return (await res.json()) as Project;
  }

  /** Get a project by ID. */
  async getProject(projectId: string): Promise<Project> {
    const res = await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}`);
    return (await res.json()) as Project;
  }

  /** Update a project. */
  async updateProject(projectId: string, request: UpdateProjectRequest): Promise<Project> {
    const res = await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}`, {
      method: "PUT",
      body: JSON.stringify(request),
    });
    return (await res.json()) as Project;
  }

  /** Delete a project (admin only). */
  async deleteProject(projectId: string): Promise<void> {
    await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}`, {
      method: "DELETE",
    });
  }

  /** Archive a project. */
  async archiveProject(projectId: string): Promise<void> {
    await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}/archive`, {
      method: "POST",
    });
  }

  /** Unarchive a project. */
  async unarchiveProject(projectId: string): Promise<void> {
    await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}/unarchive`, {
      method: "POST",
    });
  }

  /** List members of a project. */
  async listProjectMembers(projectId: string): Promise<{ members: ProjectMember[] }> {
    const res = await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}/members`);
    return (await res.json()) as { members: ProjectMember[] };
  }

  /** Add a member to a project (admin only). */
  async addProjectMember(projectId: string, request: AddProjectMemberRequest): Promise<void> {
    await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}/members`, {
      method: "POST",
      body: JSON.stringify(request),
    });
  }

  /** Remove a member from a project (admin only). */
  async removeProjectMember(projectId: string, userId: string): Promise<void> {
    await this.fetchWithRetry(
      `/v1/projects/${encodeURIComponent(projectId)}/members/${encodeURIComponent(userId)}`,
      { method: "DELETE" }
    );
  }

  /** Get project configuration. */
  async getProjectConfig(
    projectId: string,
    options?: { resolved?: boolean }
  ): Promise<{ config: Record<string, string> }> {
    const qs = options?.resolved ? "?resolved=true" : "";
    const res = await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}/config${qs}`);
    return (await res.json()) as { config: Record<string, string> };
  }

  /** Update project configuration. */
  async updateProjectConfig(projectId: string, values: Record<string, string>): Promise<void> {
    await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}/config`, {
      method: "PUT",
      body: JSON.stringify(values),
    });
  }

  /** List session IDs in a project. */
  async listProjectSessions(projectId: string): Promise<{ sessions: string[] }> {
    const res = await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}/sessions`);
    return (await res.json()) as { sessions: string[] };
  }

  /** Get project usage stats. */
  async getProjectUsage(projectId: string): Promise<ProjectUsage> {
    const res = await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}/usage`);
    return (await res.json()) as ProjectUsage;
  }

  /** Get project memory entries. */
  async getProjectMemory(
    projectId: string,
    layer?: string
  ): Promise<{ memory: WorkspaceMemoryEntry[] }> {
    const qs = layer ? `?layer=${encodeURIComponent(layer)}` : "";
    const res = await this.fetchWithRetry(`/v1/projects/${encodeURIComponent(projectId)}/memory${qs}`);
    return (await res.json()) as { memory: WorkspaceMemoryEntry[] };
  }

  // ── Teams ─────────────────────────────────────────────────────────────

  /** Create a team. */
  async createTeam(request: CreateTeamRequest): Promise<Team> {
    const res = await this.fetchWithRetry("/v1/teams", {
      method: "POST",
      body: JSON.stringify(request),
    });
    return (await res.json()) as Team;
  }

  /** List teams, optionally filtered by workspace. */
  async listTeams(workspaceId?: string): Promise<{ teams: Team[] }> {
    const qs = workspaceId ? `?workspaceId=${encodeURIComponent(workspaceId)}` : "";
    const res = await this.fetchWithRetry(`/v1/teams${qs}`);
    return (await res.json()) as { teams: Team[] };
  }

  /** Get a team by ID, including its members. */
  async getTeam(teamId: string): Promise<{ team: Team; members: TeamMember[] }> {
    const res = await this.fetchWithRetry(`/v1/teams/${encodeURIComponent(teamId)}`);
    return (await res.json()) as { team: Team; members: TeamMember[] };
  }

  /** Delete a team. */
  async deleteTeam(teamId: string): Promise<void> {
    await this.fetchWithRetry(`/v1/teams/${encodeURIComponent(teamId)}`, {
      method: "DELETE",
    });
  }

  /** Add a member to a team. */
  async addTeamMember(teamId: string, request: AddTeamMemberRequest): Promise<AddTeamMemberResponse> {
    const res = await this.fetchWithRetry(`/v1/teams/${encodeURIComponent(teamId)}/members`, {
      method: "POST",
      body: JSON.stringify(request),
    });
    return (await res.json()) as AddTeamMemberResponse;
  }

  /** List members of a team. */
  async listTeamMembers(teamId: string): Promise<{ members: TeamMember[] }> {
    const res = await this.fetchWithRetry(`/v1/teams/${encodeURIComponent(teamId)}/members`);
    return (await res.json()) as { members: TeamMember[] };
  }

  /** Start a team run. */
  async runTeam(teamId: string, request: TeamRunRequest): Promise<TeamRunResponse> {
    const res = await this.fetchWithRetry(`/v1/teams/${encodeURIComponent(teamId)}/runs`, {
      method: "POST",
      body: JSON.stringify(request),
    });
    return (await res.json()) as TeamRunResponse;
  }

  /**
   * Update a team's run profile (Phase 78 Path 2).
   * PATCH-style: any field omitted (or set to null) keeps its current value.
   */
  async updateTeamProfile(teamId: string, request: UpdateTeamProfileRequest): Promise<{ team: Team }> {
    const res = await this.fetchWithRetry(`/v1/teams/${encodeURIComponent(teamId)}/profile`, {
      method: "PUT",
      body: JSON.stringify(request),
    });
    return (await res.json()) as { team: Team };
  }

  // ── Runs ──────────────────────────────────────────────────────────────

  /** Get a run by ID. */
  async getRun(runId: string): Promise<AgentRun> {
    const res = await this.fetchWithRetry(`/v1/runs/${encodeURIComponent(runId)}`);
    return (await res.json()) as AgentRun;
  }

  /** List runs with optional filters. */
  async listRuns(filter?: AgentRunFilter): Promise<{ runs: AgentRun[] }> {
    const params = new URLSearchParams();
    if (filter?.workspace_id) params.set("workspaceId", filter.workspace_id);
    if (filter?.user_id) params.set("userId", filter.user_id);
    if (filter?.team_id) params.set("teamId", filter.team_id);
    if (filter?.kind) params.set("kind", filter.kind);
    if (filter?.status) params.set("status", filter.status);
    if (filter?.limit !== undefined) params.set("limit", String(filter.limit));
    const qs = params.toString();
    const res = await this.fetchWithRetry(`/v1/runs${qs ? `?${qs}` : ""}`);
    return (await res.json()) as { runs: AgentRun[] };
  }

  // ── Missions ──────────────────────────────────────────────────────────

  /** Create a mission. */
  async createMission(request: CreateMissionRequest): Promise<Mission> {
    const res = await this.fetchWithRetry("/v1/missions", {
      method: "POST",
      body: JSON.stringify(request),
    });
    return (await res.json()) as Mission;
  }

  /** List missions with optional filters. */
  async listMissions(options?: {
    ownerUserId?: string;
    status?: string;
    limit?: number;
  }): Promise<{ missions: Mission[] }> {
    const params = new URLSearchParams();
    if (options?.ownerUserId) params.set("ownerUserId", options.ownerUserId);
    if (options?.status) params.set("status", options.status);
    if (options?.limit !== undefined) params.set("limit", String(options.limit));
    const qs = params.toString();
    const res = await this.fetchWithRetry(`/v1/missions${qs ? `?${qs}` : ""}`);
    return (await res.json()) as { missions: Mission[] };
  }

  /** Get a mission by ID. */
  async getMission(missionId: string): Promise<Mission> {
    const res = await this.fetchWithRetry(`/v1/missions/${encodeURIComponent(missionId)}`);
    return (await res.json()) as Mission;
  }

  /** Drive a mission forward one engine cycle. */
  async runMission(missionId: string): Promise<Mission> {
    const res = await this.fetchWithRetry(`/v1/missions/${encodeURIComponent(missionId)}/run`, {
      method: "POST",
    });
    return (await res.json()) as Mission;
  }

  /** Get the full event journal for a mission. */
  async getMissionEvents(missionId: string): Promise<{ events: MissionEvent[] }> {
    const res = await this.fetchWithRetry(`/v1/missions/${encodeURIComponent(missionId)}/events`);
    return (await res.json()) as { events: MissionEvent[] };
  }

  /** Export a mission as markdown or JSON. */
  async exportMission(missionId: string, format: "markdown" | "json" = "markdown"): Promise<string> {
    if (!ALLOWED_EXPORT_FORMATS.includes(format)) {
      throw new Error(
        `Invalid export format "${format}". Allowed: ${ALLOWED_EXPORT_FORMATS.join(", ")}`
      );
    }
    const qs = format === "json" ? `?format=${encodeURIComponent(format)}` : "";
    const res = await this.fetchWithRetry(`/v1/missions/${encodeURIComponent(missionId)}/export${qs}`);
    return res.text();
  }

  // ── Swarm ─────────────────────────────────────────────────────────────

  /**
   * Run a swarm and stream events via SSE (POST /v1/swarm).
   *
   * The server emits SSE messages of the form `event: <type>\ndata: <json>`.
   * This method parses both fields and dispatches them through
   * {@link SwarmStreamCallbacks}. The `event` field for swarm events is the
   * C# record type name (e.g. `"PlanCreated"`, `"TaskStarted"`); the special
   * events `"plan"` (dry runs), `"result"` (final swarm result), and
   * `"error"` are also emitted.
   *
   * NOTE: This SSE wire format uses PascalCase property names because the
   * server serialises swarm events with no naming policy. The shape of the
   * `data` payload mirrors {@link SwarmEvent} / {@link SwarmResult} but with
   * the property casing flipped to PascalCase.
   */
  async runSwarm(
    request: SwarmRunRequest,
    callbacks: SwarmStreamCallbacks
  ): Promise<void> {
    const res = await this.fetchWithRetry("/v1/swarm", {
      method: "POST",
      body: JSON.stringify(request),
    });
    try {
      for await (const evt of parseTaggedSseStream(res)) {
        try { await callbacks.onEvent?.(evt); } catch { /* callback error — non-fatal */ }
        if (evt.event === "result") {
          try { await callbacks.onResult?.(evt.data); } catch { /* non-fatal */ }
        } else if (evt.event === "error") {
          try { await callbacks.onServerError?.(evt.data); } catch { /* non-fatal */ }
        }
      }
      await callbacks.onComplete?.();
    } catch (err) {
      await callbacks.onError?.(
        err instanceof Error ? err : new Error(String(err))
      );
    }
  }

  /** Get swarm result by ID. */
  async getSwarm(swarmId: string): Promise<SwarmResult> {
    const res = await this.fetchWithRetry(`/v1/swarm/${encodeURIComponent(swarmId)}`);
    return (await res.json()) as SwarmResult;
  }

  /** Replay events for a swarm session. */
  async getSwarmEvents(swarmId: string): Promise<SwarmEvent[]> {
    const res = await this.fetchWithRetry(`/v1/swarm/${encodeURIComponent(swarmId)}/events`);
    return (await res.json()) as SwarmEvent[];
  }

  /** List swarm sessions with optional filters. */
  async listSwarmSessions(options?: {
    workspace_id?: string;
    project_id?: string;
    limit?: number;
  }): Promise<string[]> {
    const params = new URLSearchParams();
    if (options?.workspace_id) params.set("workspace_id", options.workspace_id);
    if (options?.project_id) params.set("project_id", options.project_id);
    if (options?.limit !== undefined) params.set("limit", String(options.limit));
    const qs = params.toString();
    const res = await this.fetchWithRetry(`/v1/swarm/sessions${qs ? `?${qs}` : ""}`);
    return (await res.json()) as string[];
  }

  // ── Engine ────────────────────────────────────────────────────────────

  /** Get the full trace for an engine run. */
  async getEngineTrace(runtimeRunId: string): Promise<{ runtime_run_id: string; entries: RuntimeTraceEntry[] }> {
    const res = await this.fetchWithRetry(`/v1/engine/runs/${encodeURIComponent(runtimeRunId)}/trace`);
    return (await res.json()) as { runtime_run_id: string; entries: RuntimeTraceEntry[] };
  }

  /** List engine runs that crashed mid-step. */
  async listInFlightRuns(): Promise<{ runtime_run_ids: string[] }> {
    const res = await this.fetchWithRetry("/v1/engine/runs/in-flight");
    return (await res.json()) as { runtime_run_ids: string[] };
  }

  /** Recover crashed engine runs. */
  async recoverEngineRuns(): Promise<{ recovered: string[] }> {
    const res = await this.fetchWithRetry("/v1/engine/runs/recover", { method: "POST" });
    return (await res.json()) as { recovered: string[] };
  }

  /** Delete all trace rows for an engine run. */
  async deleteEngineRun(runtimeRunId: string): Promise<{ runtime_run_id: string; deleted_rows: number }> {
    const res = await this.fetchWithRetry(`/v1/engine/runs/${encodeURIComponent(runtimeRunId)}`, {
      method: "DELETE",
    });
    return (await res.json()) as { runtime_run_id: string; deleted_rows: number };
  }

  // ── Evals ─────────────────────────────────────────────────────────────

  /** List available eval suites. */
  async listEvals(): Promise<EvalSuite[]> {
    const res = await this.fetchWithRetry("/v1/evals");
    return (await res.json()) as EvalSuite[];
  }

  /** Get eval history for a suite. */
  async getEvalHistory(suiteName: string): Promise<EvalReportSummary[]> {
    const res = await this.fetchWithRetry(`/v1/evals/${encodeURIComponent(suiteName)}/history`);
    return (await res.json()) as EvalReportSummary[];
  }

  /** Run an eval suite. */
  async runEval(request: EvalRunRequest): Promise<EvalRunResponse> {
    const res = await this.fetchWithRetry("/v1/evals/run", {
      method: "POST",
      body: JSON.stringify(request),
    });
    return (await res.json()) as EvalRunResponse;
  }

  // ── Artifacts ─────────────────────────────────────────────────────────

  /** List artifacts with optional scope filters. */
  async listArtifacts(scope?: ArtifactScope): Promise<{ artifacts: ArtifactEntry[]; count: number }> {
    const params = new URLSearchParams();
    if (scope?.workspace_id) params.set("workspace_id", scope.workspace_id);
    if (scope?.project_id) params.set("project_id", scope.project_id);
    if (scope?.run_id) params.set("run_id", scope.run_id);
    const qs = params.toString();
    const res = await this.fetchWithRetry(`/v1/artifacts${qs ? `?${qs}` : ""}`);
    return (await res.json()) as { artifacts: ArtifactEntry[]; count: number };
  }

  /**
   * Download a single artifact file as a fetch {@link Response}.
   *
   * The body is a binary stream (or text, depending on content type). Use
   * `await res.arrayBuffer()`, `await res.blob()`, or `await res.text()` to
   * consume it. The `Content-Type` and `Content-Disposition` headers from
   * the server are preserved on the returned response.
   */
  async getArtifact(
    runId: string,
    path: string,
    scope?: { workspace_id?: string; project_id?: string }
  ): Promise<Response> {
    const params = new URLSearchParams();
    if (scope?.workspace_id) params.set("workspace_id", scope.workspace_id);
    if (scope?.project_id) params.set("project_id", scope.project_id);
    const qs = params.toString();
    // Encode each path segment but keep the slashes for the catch-all route.
    const encodedPath = path
      .split("/")
      .map(encodeURIComponent)
      .join("/");
    return this.fetchWithRetry(
      `/v1/artifacts/${encodeURIComponent(runId)}/${encodedPath}${qs ? `?${qs}` : ""}`
    );
  }

  /** Delete all artifacts for a run. */
  async deleteArtifacts(runId: string, scope?: ArtifactScope): Promise<void> {
    const params = new URLSearchParams();
    if (scope?.workspace_id) params.set("workspace_id", scope.workspace_id);
    if (scope?.project_id) params.set("project_id", scope.project_id);
    const qs = params.toString();
    await this.fetchWithRetry(`/v1/artifacts/${encodeURIComponent(runId)}${qs ? `?${qs}` : ""}`, {
      method: "DELETE",
    });
  }

  // ── Tool Registry ─────────────────────────────────────────────────────

  /** List all registered tools. */
  async listTools(): Promise<{ tools: ToolDefinition[]; count: number }> {
    const res = await this.fetchWithRetry("/v1/tools");
    return (await res.json()) as { tools: ToolDefinition[]; count: number };
  }

  /** Get a tool by name. */
  async getTool(name: string): Promise<ToolDefinition> {
    const res = await this.fetchWithRetry(`/v1/tools/${encodeURIComponent(name)}`);
    return (await res.json()) as ToolDefinition;
  }

  // ── Skill Registry ────────────────────────────────────────────────────

  /** List all registered skills. */
  async listSkills(): Promise<{ skills: SkillSummary[]; count: number }> {
    const res = await this.fetchWithRetry("/v1/skills");
    return (await res.json()) as { skills: SkillSummary[]; count: number };
  }

  /** Get a skill by name (includes body). */
  async getSkill(name: string): Promise<SkillDetail> {
    const res = await this.fetchWithRetry(`/v1/skills/${encodeURIComponent(name)}`);
    return (await res.json()) as SkillDetail;
  }

  // ── Agent Templates ───────────────────────────────────────────────────

  /** List all agent templates. */
  async listAgentTemplates(): Promise<{ templates: AgentTemplateSummary[]; count: number }> {
    const res = await this.fetchWithRetry("/v1/agents/templates");
    return (await res.json()) as { templates: AgentTemplateSummary[]; count: number };
  }

  /** Get an agent template by name. */
  async getAgentTemplate(name: string): Promise<AgentTemplateDetail> {
    const res = await this.fetchWithRetry(`/v1/agents/templates/${encodeURIComponent(name)}`);
    return (await res.json()) as AgentTemplateDetail;
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
      ...extraHeaders,
    };
    // Only set Content-Type when there is a request body to avoid
    // confusing proxies/servers on GET and DELETE requests.
    if (init?.body !== undefined) {
      headers["Content-Type"] = "application/json";
    }

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

/** Maximum length of response body included in error messages to avoid leaking server internals. */
const MAX_ERROR_BODY_LENGTH = 256;

/** Error thrown when the Sovrant API returns a non-OK response. */
export class SovrantApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly body: string,
    public readonly url: string
  ) {
    const truncated =
      body.length > MAX_ERROR_BODY_LENGTH
        ? body.slice(0, MAX_ERROR_BODY_LENGTH) + "…"
        : body;
    super(`Sovrant API error ${status} from ${url}: ${truncated}`);
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

/** Maximum SSE buffer size for tagged streams (1 MB). */
const TAGGED_SSE_MAX_BUFFER = 1 * 1024 * 1024;

/** Per-read timeout for tagged SSE streams (5 minutes). */
const TAGGED_SSE_READ_TIMEOUT_MS = 5 * 60 * 1000;

/**
 * Parse an SSE stream that uses both `event:` and `data:` lines (e.g. swarm).
 * Yields an object per fully-formed event with the event type and parsed JSON.
 * Stops on `data: [DONE]`.
 */
async function* parseTaggedSseStream(
  response: Response
): AsyncGenerator<SwarmSseEvent> {
  const reader = response.body?.getReader();
  if (!reader) throw new Error("Response body is not readable.");

  const decoder = new TextDecoder();
  let buffer = "";

  try {
    while (true) {
      const readPromise = reader.read();
      const timeout = new Promise<never>((_, reject) =>
        setTimeout(() => reject(new Error("SSE read timed out")), TAGGED_SSE_READ_TIMEOUT_MS)
      );
      const { done, value } = await Promise.race([readPromise, timeout]);
      if (done) break;

      buffer += decoder.decode(value, { stream: true });

      if (buffer.length > TAGGED_SSE_MAX_BUFFER) {
        throw new Error(
          `SSE buffer exceeded maximum size of ${TAGGED_SSE_MAX_BUFFER} bytes.`
        );
      }

      const parts = buffer.split("\n\n");
      buffer = parts.pop() ?? "";

      for (const part of parts) {
        let eventType = "message";
        let dataLine: string | undefined;
        for (const rawLine of part.split("\n")) {
          const line = rawLine.trimEnd();
          if (line.startsWith("event: ")) {
            eventType = line.slice("event: ".length).trim();
          } else if (line.startsWith("data: ")) {
            dataLine = (dataLine ?? "") + line.slice("data: ".length);
          }
        }
        if (dataLine === undefined) continue;
        if (dataLine === "[DONE]") return;
        try {
          const parsed = JSON.parse(dataLine, (key, value) => {
            if (key === "__proto__" || key === "constructor" || key === "prototype") {
              return undefined;
            }
            return value as unknown;
          }) as unknown;
          yield { event: eventType, data: parsed };
        } catch {
          // Skip malformed event silently.
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}

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
