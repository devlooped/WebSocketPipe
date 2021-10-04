using System;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Devlooped.Net;

/// <summary>
/// A <see cref="IDuplexPipe"/> over a <see cref="WebSocket"/> which can be 
/// used for reading and writing to it.
/// </summary>
public interface IWebSocketPipe : IDuplexPipe, IDisposable
{
    /// <summary>
    /// Indicates the reason for the close handshake.
    /// </summary>
    public WebSocketCloseStatus? CloseStatus { get; }

    /// <summary>
    /// Allows describing the reason why the connection was closed.
    /// </summary>
    public string? CloseStatusDescription { get; }

    /// <summary>
    /// Returns the current state of the underlying <see cref="WebSocket"/> connection.
    /// </summary>
    public WebSocketState State { get; }

    /// <summary>
    /// Gets the subprotocol that was negotiated during the opening handshake.
    /// </summary>
    public string? SubProtocol { get; }

    /// <summary>
    /// Completes both the <see cref="IDuplexPipe.Input"/> and <see cref="IDuplexPipe.Output"/> 
    /// of the duplex pipe, and optionally closes the underlying <see cref="WebSocket"/> with the 
    /// specified status and reason.
    /// </summary>
    /// <param name="closeStatus">Optional close status to use for closing the underlying <see cref="WebSocket"/>. 
    /// If no status is provided, the socket will be closed only if <see cref="WebSocketPipeOptions.CloseWhenCompleted"/> 
    /// was specified when the duplex pipe was created.
    /// </param>
    /// <param name="closeStatusDescription">Optional close status description to use if the underlying 
    /// <see cref="WebSocket"/> is closed.</param>
    /// <returns></returns>
    public Task CompleteAsync(WebSocketCloseStatus? closeStatus = null, string? closeStatusDescription = null);

    /// <summary>
    /// Starts populating the <see cref="IDuplexPipe.Input"/> with incoming data from the underlying 
    /// <see cref="WebSocket"/> as well as sending outgoing data written to the <see cref="IDuplexPipe.Output"/> 
    /// and finishes when either are completed (or an explicit call to <see cref="CompleteAsync"/> is performed).
    /// </summary>
    /// <param name="cancellation">Optional cancellation token to complete the operation.</param>
    /// <returns>
    /// A <see cref="Task"/> that will be completed when either <see cref="IDuplexPipe.Input" /> or 
    /// <see cref="IDuplexPipe.Output"/> are completed, or an explicit invocation of <see cref="CompleteAsync"/> 
    /// is executed.
    /// </returns>
    public Task RunAsync(CancellationToken cancellation = default);
}
