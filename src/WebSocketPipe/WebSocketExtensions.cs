using System.ComponentModel;
using System.IO.Pipelines;
using Devlooped.Net;

namespace System.Net.WebSockets;

/// <summary>
/// Provides the <see cref="CreatePipe"/> extension method for 
/// reading/writing to a <see cref="WebSocket"/> using the <see cref="IDuplexPipe"/> (and 
/// <see cref="IWebSocketPipe"/>) API.
/// API.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class WebSocketExtensions
{
    /// <summary>
    /// Creates a <see cref="IWebSocketPipe"/> over the given <paramref name="webSocket"/>.
    /// </summary>
    /// <param name="webSocket">The <see cref="WebSocket"/> to adapt.</param>
    /// <param name="closeWhenCompleted">Whether to close the <paramref name="webSocket"/> when 
    /// either the <see cref="IDuplexPipe.Input"/> or <see cref="IDuplexPipe.Output"/> is 
    /// completed, or when <see cref="IWebSocketPipe.CompleteAsync"/> is invoked.</param>
    public static IWebSocketPipe CreatePipe(this WebSocket webSocket, bool closeWhenCompleted = false)
        => WebSocketPipe.Create(webSocket, closeWhenCompleted ? new WebSocketPipeOptions { CloseWhenCompleted = true } : WebSocketPipeOptions.Default);
}

