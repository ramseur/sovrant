using System.Text.Json;

namespace Sovrant.Api.Errors;

/// <summary>Domain error from the LLM API layer.</summary>
public sealed class ApiError : Exception
{
    /// <summary>Initializes a new instance of <see cref="ApiError"/>.</summary>
    public ApiError() : base("An LLM API error occurred.") { }
    /// <summary>Initializes a new instance of <see cref="ApiError"/> with the specified message.</summary>
    public ApiError(string message) : base(message) { }
    /// <summary>Initializes a new instance of <see cref="ApiError"/> with message and inner exception.</summary>
    public ApiError(string message, Exception? inner) : base(message, inner) { }
    /// <summary>Indicates whether the operation can be retried.</summary>
    public bool IsRetryable { get; private init; }
    /// <summary>Creates an error for missing API credentials.</summary>
    public static ApiError MissingCredentials(string provider, params string[] envVars) =>
        new($"Missing credentials for {provider}. Set one of: {string.Join(", ", envVars)}");
    /// <summary>Creates an authentication error.</summary>
    public static ApiError Auth(string message) => new($"Authentication failed: {message}");
    /// <summary>Creates an error wrapping an <see cref="HttpRequestException"/>.</summary>
    public static ApiError Http(HttpRequestException ex) { ArgumentNullException.ThrowIfNull(ex); return new ApiError(ex.Message, ex) { IsRetryable = true }; }
    /// <summary>Creates an error wrapping a <see cref="JsonException"/>.</summary>
    public static ApiError Json(JsonException ex) { ArgumentNullException.ThrowIfNull(ex); return new ApiError($"JSON error: {ex.Message}", ex); }
    /// <summary>Creates an error from an API error response.</summary>
    public static ApiError Api(int status, string? errorType, string? message, string body, bool retryable) =>
        new($"API error {status} ({errorType ?? "unknown"}): {message ?? body}") { IsRetryable = retryable };
    /// <summary>Creates an error indicating all retries were exhausted.</summary>
    public static ApiError RetriesExhausted(int attempts) => new($"All {attempts} retry attempts exhausted.");
    /// <summary>Creates an error for a malformed SSE frame.</summary>
    public static ApiError InvalidSseFrame(string reason) => new($"Invalid SSE frame: {reason}");
    /// <summary>Creates an error wrapping an <see cref="IOException"/>.</summary>
    public static ApiError IoError(IOException ex) { ArgumentNullException.ThrowIfNull(ex); return new ApiError($"IO error: {ex.Message}", ex); }
}
