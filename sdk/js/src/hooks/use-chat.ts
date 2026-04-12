import { useState, useCallback, useRef, useEffect } from "react";
import { SovrantClient } from "../client.js";
import type {
  SovrantClientOptions,
  SovrantEvent,
  UsageInfo,
} from "../types.js";

/** A single message in the chat UI. */
export interface ChatEntry {
  id: string;
  role: "user" | "assistant";
  content: string;
  /** Tool events associated with this assistant message. */
  toolEvents?: SovrantEvent[];
  usage?: UsageInfo;
  createdAt: Date;
}

/** Options for the useChat hook. */
export interface UseChatOptions extends SovrantClientOptions {
  /** Initial messages to populate the chat. */
  initialMessages?: ChatEntry[];
  /** Called when a tool invocation starts. */
  onToolUse?: (event: SovrantEvent) => void;
  /** Called when a tool invocation completes. */
  onToolResult?: (event: SovrantEvent) => void;
  /** Called on errors. */
  onError?: (error: Error) => void;
}

/** Return type of the useChat hook. */
export interface UseChatReturn {
  /** All messages in the conversation. */
  messages: ChatEntry[];
  /** Whether a response is currently streaming. */
  isStreaming: boolean;
  /** Send a message and stream the response. */
  send: (message: string) => Promise<void>;
  /** Clear all messages. */
  clear: () => void;
  /** The last error that occurred, if any. */
  error: Error | null;
}

let nextId = 0;
function genId(): string {
  return `msg-${Date.now()}-${nextId++}`;
}

/**
 * React hook for streaming chat with a Sovrant server.
 *
 * @example
 * ```tsx
 * function Chat() {
 *   const { messages, send, isStreaming } = useChat({
 *     baseUrl: "http://localhost:5200",
 *     token: "your-token",
 *     model: "gpt-4o-mini",
 *   });
 *
 *   return (
 *     <div>
 *       {messages.map((m) => (
 *         <div key={m.id}>{m.role}: {m.content}</div>
 *       ))}
 *       <input
 *         onKeyDown={(e) => {
 *           if (e.key === "Enter") send(e.currentTarget.value);
 *         }}
 *         disabled={isStreaming}
 *       />
 *     </div>
 *   );
 * }
 * ```
 */
export function useChat(options: UseChatOptions): UseChatReturn {
  const [messages, setMessages] = useState<ChatEntry[]>(
    options.initialMessages ?? []
  );
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  // Recreate client when connection-relevant options change.
  const clientRef = useRef<SovrantClient | null>(null);
  const {
    baseUrl,
    token,
    model,
    sessionId,
    maxRetries,
    timeoutMs,
    llmApiKey,
    llmBaseUrl,
  } = options;

  useEffect(() => {
    clientRef.current = new SovrantClient(options);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- track individual fields
  }, [baseUrl, token, model, sessionId, maxRetries, timeoutMs, llmApiKey, llmBaseUrl]);

  // Ensure a client exists on first render (before the effect fires).
  if (!clientRef.current) {
    clientRef.current = new SovrantClient(options);
  }

  const send = useCallback(
    async (message: string) => {
      if (!message.trim() || isStreaming) return;

      setError(null);

      // Add user message.
      const userEntry: ChatEntry = {
        id: genId(),
        role: "user",
        content: message,
        createdAt: new Date(),
      };

      // Prepare assistant placeholder.
      const assistantId = genId();
      const assistantEntry: ChatEntry = {
        id: assistantId,
        role: "assistant",
        content: "",
        toolEvents: [],
        createdAt: new Date(),
      };

      setMessages((prev) => [...prev, userEntry, assistantEntry]);
      setIsStreaming(true);

      try {
        await clientRef.current!.stream(message, {
          onText: (text) => {
            setMessages((prev) =>
              prev.map((m) =>
                m.id === assistantId
                  ? { ...m, content: m.content + text }
                  : m
              )
            );
          },
          onToolUse: (event) => {
            options.onToolUse?.(event);
            setMessages((prev) =>
              prev.map((m) =>
                m.id === assistantId
                  ? { ...m, toolEvents: [...(m.toolEvents ?? []), event] }
                  : m
              )
            );
          },
          onToolResult: (event) => {
            options.onToolResult?.(event);
            setMessages((prev) =>
              prev.map((m) =>
                m.id === assistantId
                  ? { ...m, toolEvents: [...(m.toolEvents ?? []), event] }
                  : m
              )
            );
          },
          onComplete: ({ usage }) => {
            setMessages((prev) =>
              prev.map((m) =>
                m.id === assistantId ? { ...m, usage } : m
              )
            );
          },
          onError: (err) => {
            setError(err);
            options.onError?.(err);
          },
        });
      } catch (err) {
        const e = err instanceof Error ? err : new Error(String(err));
        setError(e);
        options.onError?.(e);
      } finally {
        setIsStreaming(false);
      }
    },
    [isStreaming, options]
  );

  const clear = useCallback(() => {
    setMessages([]);
    setError(null);
  }, []);

  return { messages, isStreaming, send, clear, error };
}
