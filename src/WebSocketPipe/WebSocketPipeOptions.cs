using System.IO.Pipelines;
using System.Net.WebSockets;

namespace Devlooped.Net;

/// <summary>
/// Configures the behavior of <see cref="IWebSocketPipe"/>.
/// </summary>
public class WebSocketPipeOptions
{
    /// <summary>
    /// Default options for <see cref="WebSocketPipe"/>.
    /// </summary>
    public static WebSocketPipeOptions Default { get; } = new WebSocketPipeOptions();

    /// <summary>
    /// Whether to close the underlying <see cref="WebSocket"/> when 
    /// either the <see cref="IDuplexPipe.Input"/> or <see cref="IDuplexPipe.Output"/> 
    /// is marked as completed. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Unless overriden in an explicit call to <see cref="IWebSocketPipe.CompleteAsync"/>, 
    /// the close status will be <see cref="WebSocketCloseStatus.NormalClosure"/>.
    /// </remarks>
    public bool CloseWhenCompleted { get; set; }

    /// <summary>
    /// Allows fine-grained configuration options for the incoming side of the 
    /// websocket pipe. Defaults to <see cref="PipeOptions.Default"/>.
    /// </summary>
    public PipeOptions InputPipeOptions { get; set; } = new PipeOptions(useSynchronizationContext: false);
}