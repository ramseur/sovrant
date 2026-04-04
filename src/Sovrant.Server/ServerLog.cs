using Microsoft.Extensions.Logging;

namespace Sovrant.Server;

/// <summary>
/// Source-generated log delegates for server lifecycle events.
/// </summary>
internal static partial class ServerLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Sovrant Server ready — http://127.0.0.1:{Port}")]
    public static partial void LogServerReady(ILogger logger, int port);
}
