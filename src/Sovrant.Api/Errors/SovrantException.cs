namespace Sovrant.Api.Errors;

/// <summary>
/// Base class for exceptions thrown by Sovrant's own code paths. Lets callers
/// distinguish domain errors originating in Sovrant from framework exceptions
/// (<see cref="IOException"/>, <see cref="HttpRequestException"/>, etc.) without
/// catching the broad <see cref="Exception"/> base.
/// </summary>
public class SovrantException : Exception
{
    public SovrantException() { }
    public SovrantException(string message) : base(message) { }
    public SovrantException(string message, Exception? inner) : base(message, inner) { }
}
