using Sovrant.Api.Errors;

namespace Sovrant.Api;

/// <summary>Rust-style discriminated result for error propagation without exceptions.</summary>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private Result(T? value, ApiError? error, bool isSuccess) { Value = value; Error = error; IsSuccess = isSuccess; }
    /// <summary>Whether this result represents a success.</summary>
    public bool IsSuccess { get; }
    /// <summary>The success value; only valid when <see cref="IsSuccess"/> is <c>true</c>.</summary>
    public T? Value { get; }
    /// <summary>The error; only valid when <see cref="IsSuccess"/> is <c>false</c>.</summary>
    public ApiError? Error { get; }
    /// <summary>Creates a successful result.</summary>
    public static Result<T> Ok(T value) => new(value, null, true);
    /// <summary>Creates a failed result.</summary>
    public static Result<T> Fail(ApiError error) => new(default, error, false);
    /// <summary>Implicitly wraps a value in a successful result.</summary>
    public static implicit operator Result<T>(T value) => Ok(value);
    /// <summary>Throws if failed, returns value if success.</summary>
    public T ThrowIfFailed() { if (!IsSuccess) { if (Error is not null) throw Error; throw new InvalidOperationException("Result is a failure but contains no error."); } return Value!; }
    /// <inheritdoc/>
    public bool Equals(Result<T> other) => IsSuccess == other.IsSuccess && EqualityComparer<T?>.Default.Equals(Value, other.Value) && ReferenceEquals(Error, other.Error);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(IsSuccess, Value, Error);
    /// <summary>Returns true if two results are equal.</summary>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
    /// <summary>Returns true if two results are not equal.</summary>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);
}
