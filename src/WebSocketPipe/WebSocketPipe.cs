using System.IO.Pipelines;
using System.Net.WebSockets;

namespace Devlooped.Net;

/// <summary>
/// Factory class for <see cref="IWebSocketPipe"/> instances.
/// </summary>
public static class WebSocketPipe
{
    /// <summary>
    /// Creates a <see cref="IWebSocketPipe"/> over the given <paramref name="webSocket"/>.
    /// </summary>
    /// <param name="webSocket">The <see cref="WebSocket"/> to adapt.</param>
    /// <param name="closeWhenCompleted">Whether to close the <paramref name="webSocket"/> when 
    /// either the <see cref="IDuplexPipe.Input"/> or <see cref="IDuplexPipe.Output"/> is 
    /// completed, or when <see cref="IWebSocketPipe.CompleteAsync"/> is invoked.</param>
    public static IWebSocketPipe Create(WebSocket webSocket, bool closeWhenCompleted = false)
        => Create(webSocket, closeWhenCompleted ? new WebSocketPipeOptions { CloseWhenCompleted = true } : WebSocketPipeOptions.Default);

    /// <summary>
    /// Creates a <see cref="IWebSocketPipe"/> over the given <paramref name="webSocket"/> using the 
    /// given <paramref name="options"/> to configure its behavior.
    /// </summary>
    /// <param name="webSocket">The <see cref="WebSocket"/> to adapt.</param>
    /// <param name="options">Configures the behavior of the <see cref="IWebSocketPipe"/>.</param>
    public static IWebSocketPipe Create(WebSocket webSocket, WebSocketPipeOptions options)
        // NOTE: when reconnection options are set, we can switch the implementation here.
        => new SimpleWebSocketPipe(webSocket, options);
}
