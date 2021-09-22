using System;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Devlooped.Net;

/// <summary>
/// Basic implementation that simply wraps <see cref="WebSocket"/> and exposes 
/// input and output pipes.
/// </summary>
class SimpleWebSocketPipe : IWebSocketPipe
{
    // Wait 250 ms before giving up on a Close, same as SignalR WebSocketHandler
    static readonly TimeSpan closeTimeout = TimeSpan.FromMilliseconds(250);

    readonly Pipe inputPipe;
    readonly Pipe outputPipe;

    readonly WebSocket webSocket;
    readonly WebSocketPipeOptions options;

    bool completed;

    public SimpleWebSocketPipe(WebSocket webSocket, WebSocketPipeOptions options)
        => (this.webSocket, this.options, inputPipe, outputPipe)
        = (webSocket, options, new Pipe(options.InputPipeOptions), new Pipe(options.OutputPipeOptions));

    bool IsClient => webSocket is ClientWebSocket;

    public PipeReader Input => inputPipe.Reader;

    public PipeWriter Output => outputPipe.Writer;

    public WebSocketCloseStatus? CloseStatus => webSocket.CloseStatus;

    public string? CloseStatusDescription => webSocket.CloseStatusDescription;

    public WebSocketState State => webSocket.State;

    public string? SubProtocol => webSocket.SubProtocol;

    public async ValueTask RunAsync(CancellationToken cancellation = default)
    {
        if (webSocket.State != WebSocketState.Open)
            throw new InvalidOperationException($"WebSocket must be opened. State was {webSocket.State}");

        var writing = FillInputAsync(cancellation);
        var reading = SendOutputAsync(cancellation);

        // NOTE: when both are completed, the CompleteAsync will be called automatically 
        // by both writing and reading, so we ensure CloseWhenCompleted is performed.

        // TODO: replace with ValueTask.WhenAll if/when it ships. 
        // See https://github.com/dotnet/runtime/issues/23625
        await Task.WhenAll(reading.AsTask(), writing.AsTask());
    }

    public async ValueTask CompleteAsync(WebSocketCloseStatus? closeStatus = null, string? closeStatusDescription = null)
    {
        if (completed)
            return;

        completed = true;

        // NOTE: invoking these more than once is no-op.
        await inputPipe.Writer.CompleteAsync();
        await inputPipe.Reader.CompleteAsync();

        await outputPipe.Writer.CompleteAsync();
        await outputPipe.Reader.CompleteAsync();

        if (options.CloseWhenCompleted || closeStatus != null)
            await CloseAsync(closeStatus ?? WebSocketCloseStatus.NormalClosure, closeStatusDescription ?? "");
    }

    async ValueTask CloseAsync(WebSocketCloseStatus closeStatus, string closeStatusDescription)
    {
        var state = State;
        if (state == WebSocketState.Closed || state == WebSocketState.CloseSent || state == WebSocketState.Aborted)
            return;

        var closeTask = IsClient ?
            // Disconnect from client vs server is different.
            webSocket.CloseAsync(closeStatus, closeStatusDescription, default) :
            webSocket.CloseOutputAsync(closeStatus, closeStatusDescription, default);

        // Don't wait indefinitely for the close to be acknowledged
        await Task.WhenAny(closeTask, Task.Delay(closeTimeout));
    }

    async ValueTask FillInputAsync(CancellationToken cancellation)
    {
        while (webSocket.State == WebSocketState.Open && !cancellation.IsCancellationRequested)
        {
            try
            {
                var message = await webSocket.ReceiveAsync(inputPipe.Writer.GetMemory(512), cancellation);
                while (!cancellation.IsCancellationRequested && !message.EndOfMessage && message.MessageType != WebSocketMessageType.Close)
                {
                    if (message.Count == 0)
                        break;

                    inputPipe.Writer.Advance(message.Count);
                    message = await webSocket.ReceiveAsync(inputPipe.Writer.GetMemory(512), cancellation);
                }

                // We didn't get a complete message, we can't flush partial message.
                if (cancellation.IsCancellationRequested || !message.EndOfMessage || message.MessageType == WebSocketMessageType.Close)
                    break;

                // Advance the EndOfMessage bytes before flushing.
                inputPipe.Writer.Advance(message.Count);
                var result = await inputPipe.Writer.FlushAsync(cancellation);
                if (result.IsCompleted)
                    break;

            }
            catch (Exception ex) when (ex is OperationCanceledException ||
                                       ex is WebSocketException ||
                                       ex is InvalidOperationException)
            {
                break;
            }
        }

        // Preserve the close status since it might be triggered by a received Close message containing the status and description.
        await CompleteAsync(webSocket.CloseStatus, webSocket.CloseStatusDescription);
    }

    async ValueTask SendOutputAsync(CancellationToken cancellation)
    {
        while (webSocket.State == WebSocketState.Open && !cancellation.IsCancellationRequested)
        {
            try
            {
                var result = await outputPipe.Reader.ReadAsync(cancellation);
                if (result.IsCompleted || result.IsCanceled)
                    break;

                if (result.Buffer.IsSingleSegment)
                {
                    await webSocket.SendAsync(result.Buffer.First, WebSocketMessageType.Binary, true, cancellation);
                }
                else
                {
                    var enumerator = result.Buffer.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        // NOTE: we don't use the cancellation here because we don't want to send 
                        // partial messages from an already completely read buffer. 
                        while (true)
                        {
                            var current = enumerator.Current;
                            if (default(ReadOnlyMemory<byte>).Equals(current))
                                break;

                            // Peek next to see if we should send an end of message
                            if (enumerator.MoveNext())
                                await webSocket.SendAsync(current, WebSocketMessageType.Binary, false, cancellation);
                            else
                                await webSocket.SendAsync(current, WebSocketMessageType.Binary, true, cancellation);
                        }
                    }
                }

                outputPipe.Reader.AdvanceTo(result.Buffer.End);

            }
            catch (Exception ex) when (ex is OperationCanceledException ||
                                       ex is WebSocketException ||
                                       ex is InvalidOperationException)
            {
                break;
            }
        }

        // Preserve the close status since it might be triggered by a received Close message containing the status and description.
        await CompleteAsync(webSocket.CloseStatus, webSocket.CloseStatusDescription);
    }

    public void Dispose() => webSocket.Dispose();
}