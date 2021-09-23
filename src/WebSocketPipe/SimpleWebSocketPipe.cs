using System;
using System.IO;
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

    readonly CancellationTokenSource disposeCancellation = new CancellationTokenSource();
    readonly Pipe inputPipe;
    readonly PipeWriter outputWriter;

    readonly WebSocket webSocket;
    readonly WebSocketPipeOptions options;

    bool completed;

    public SimpleWebSocketPipe(WebSocket webSocket, WebSocketPipeOptions options)
    {
        this.webSocket = webSocket;
        this.options = options;
        inputPipe = new Pipe(options.InputPipeOptions);
        outputWriter = PipeWriter.Create(new WebSocketStream(webSocket));
    }

    bool IsClient => webSocket is ClientWebSocket;

    public PipeReader Input => inputPipe.Reader;

    public PipeWriter Output => outputWriter;

    public WebSocketCloseStatus? CloseStatus => webSocket.CloseStatus;

    public string? CloseStatusDescription => webSocket.CloseStatusDescription;

    public WebSocketState State => webSocket.State;

    public string? SubProtocol => webSocket.SubProtocol;

    public Task RunAsync(CancellationToken cancellation = default)
    {
        if (webSocket.State != WebSocketState.Open)
            throw new InvalidOperationException($"WebSocket must be opened. State was {webSocket.State}");

        var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellation, disposeCancellation.Token);
        return ReadInputAsync(combined.Token);
    }

    public async Task CompleteAsync(WebSocketCloseStatus? closeStatus = null, string? closeStatusDescription = null)
    {
        if (completed)
            return;

        completed = true;

        // NOTE: invoking these more than once is no-op.
        await inputPipe.Writer.CompleteAsync();
        await inputPipe.Reader.CompleteAsync();

        if (options.CloseWhenCompleted || closeStatus != null)
            await CloseAsync(closeStatus ?? WebSocketCloseStatus.NormalClosure, closeStatusDescription ?? "");
    }

    async Task CloseAsync(WebSocketCloseStatus closeStatus, string closeStatusDescription)
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

    async Task ReadInputAsync(CancellationToken cancellation)
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

    public void Dispose()
    {
        disposeCancellation.Cancel();
        webSocket.Dispose();
    }

    class WebSocketStream : Stream
    {
        readonly WebSocket webSocket;

        public WebSocketStream(WebSocket webSocket) => this.webSocket = webSocket;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override bool CanRead => throw new NotImplementedException();
        public override bool CanSeek => throw new NotImplementedException();
        public override bool CanWrite => throw new NotImplementedException();
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override void Flush() => throw new NotImplementedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    }
}