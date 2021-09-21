using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Devlooped
{
    public record SimpleWebSocketPipeTests(ITestOutputHelper Output)
    {
        [Fact]
        public async Task WhenWebSocketNotOpen_ThenThrowsAsync()
        {
            IWebSocketPipe pipe = WebSocketPipe.Create(new ClientWebSocket());
            await Assert.ThrowsAsync<InvalidOperationException>(() => pipe.RunAsync());
        }

        [Fact]
        public async Task WhenConnected_ThenRuns()
        {
            await using var server = WebSocketServer.Create(Echo, null, Output);
            using var socket = new ClientWebSocket();

            await socket.ConnectAsync(server.Uri, default);

            using var pipe = WebSocketPipe.Create(socket);

            await Task.WhenAll(
                pipe.RunAsync(server.Cancellation.Token),
                Task.Delay(100).ContinueWith(_ => server.Cancellation.Cancel()));
        }

        [Fact]
        public async Task WhenServerClosesWebSocket_ThenClientCompletesGracefully()
        {
            await using var server = WebSocketServer.Create(Echo, null, Output);
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(server.Uri, default);
            using var pipe = WebSocketPipe.Create(socket);
            var run = pipe.RunAsync();

            await server.DisposeAsync();

            Task.WaitAny(run, Task.Delay(100).ContinueWith(_ => throw new TimeoutException()));
        }

        [Fact]
        public async Task WhenClientClompletes_ThenServerCompletesGracefully()
        {
            IDuplexPipe? serverPipe = default;
            await using var server = WebSocketServer.Create(x =>
            {
                serverPipe = x;
                return Task.CompletedTask;
            }, null, Output);

            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(server.Uri, default);
            using var pipe = WebSocketPipe.Create(socket, closeWhenCompleted: true);
            var run = pipe.RunAsync();

            await pipe.CompleteAsync();

            Assert.Equal(WebSocketState.Closed, socket.State);
            Assert.NotNull(serverPipe);
            await Assert.ThrowsAsync<InvalidOperationException>(() => serverPipe!.Input.ReadAsync().AsTask());
        }

        [Fact]
        public async Task WhenSocketClosed_ThenCompletes()
        {
            await using var server = WebSocketServer.Create(Echo, null, Output);
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(server.Uri, default);
            using var pipe = WebSocketPipe.Create(socket);
            var run = pipe.RunAsync();

            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, default);

            Assert.Equal(WebSocketState.Closed, socket.State);
            await run;
            await Assert.ThrowsAsync<InvalidOperationException>(() => pipe.Input.ReadAsync().AsTask());
        }

        [Fact]
        public async Task WhenClientClompletesWithStatus_ThenServerAndClientShareStatus()
        {
            IWebSocketPipe? serverPipe = default;
            await using var server = WebSocketServer.Create(x =>
            {
                serverPipe = x;
                return Task.CompletedTask;
            }, null, Output);

            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(server.Uri, default);
            using var pipe = WebSocketPipe.Create(socket);
            var run = pipe.RunAsync();

            await pipe.CompleteAsync(WebSocketCloseStatus.InvalidMessageType, "Invalid");

            Assert.Equal(WebSocketState.Closed, socket.State);
            Assert.Equal(WebSocketCloseStatus.InvalidMessageType, socket.CloseStatus);
            Assert.Equal("Invalid", socket.CloseStatusDescription);

            Assert.NotNull(serverPipe);
            Assert.Equal(WebSocketState.Closed, serverPipe!.State);
            Assert.Equal(WebSocketCloseStatus.InvalidMessageType, serverPipe!.CloseStatus);
            Assert.Equal("Invalid", serverPipe!.CloseStatusDescription);
        }

        [Fact]
        public async Task WhenSubProtocolSpecified_ThenServerAndClientShareSubProtocol()
        {
            IWebSocketPipe? serverPipe = default;
            await using var server = WebSocketServer.Create(x =>
            {
                serverPipe = x;
                return Task.CompletedTask;
            }, null, Output);

            using var socket = new ClientWebSocket();
            socket.Options.AddSubProtocol("protobuf.webpubsub.azure.v1");
            await socket.ConnectAsync(server.Uri, default);
            using var pipe = WebSocketPipe.Create(socket);
            var run = pipe.RunAsync();

            Assert.Equal("protobuf.webpubsub.azure.v1", socket.SubProtocol);
            Assert.Equal("protobuf.webpubsub.azure.v1", pipe.SubProtocol);

            Assert.NotNull(serverPipe);
            Assert.Equal("protobuf.webpubsub.azure.v1", serverPipe!.SubProtocol);
        }

        [Fact]
        public async Task WhenClientClompletesWithStatus_ThenCompletesWebSocketEvenIfNotSpecified()
        {
            await using var server = WebSocketServer.Create(Echo, null, Output);
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(server.Uri, default);
            using var pipe = WebSocketPipe.Create(socket, closeWhenCompleted: false);
            var run = pipe.RunAsync();

            await pipe.CompleteAsync(WebSocketCloseStatus.InvalidMessageType, "Invalid");

            Assert.Equal(WebSocketState.Closed, socket.State);
            Assert.Equal(WebSocketCloseStatus.InvalidMessageType, socket.CloseStatus);
            Assert.Equal("Invalid", socket.CloseStatusDescription);
        }

        [Fact]
        public async Task WhenSocketDisposed_ThenStateIsClosed()
        {
            await using var server = WebSocketServer.Create(Echo, null, Output);
            using var socket = new ClientWebSocket();

            await socket.ConnectAsync(server.Uri, default);

            using var pipe = WebSocketPipe.Create(socket);

            socket.Dispose();

            Assert.Equal(WebSocketState.Closed, pipe.State);
        }

        [Fact]
        public async Task WhenReceivingChunks_PipeReaderExposesFullMessage()
        {
            await using var server = WebSocketServer.Create(async s =>
            {
                var serverPipe = WebSocketPipe.Create(s);
                var serverRun = serverPipe.RunAsync();
                while (await serverPipe.Input.ReadAsync() is var result && !result.IsCompleted)
                {
                    // Send in "chunks" of 1 byte.
                    for (var i = 0; i < result.Buffer.Length; i++)
                        await s.SendAsync(result.Buffer.Slice(i, 1).First, WebSocketMessageType.Binary, i == result.Buffer.Length - 1, default);

                    serverPipe.Input.AdvanceTo(result.Buffer.End);
                }
                await serverRun;
            }, null, Output);

            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(server.Uri, default);
            using var pipe = WebSocketPipe.Create(socket);

            var run = pipe.RunAsync();
            var write = pipe.Output.WriteAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("hello")));
            var read = await pipe.Input.ReadAsync();
            var echo = Encoding.UTF8.GetString(read.Buffer.FirstSpan);

            Assert.Equal("hello", echo);
        }

        async Task Echo(IWebSocketPipe pipe)
        {
            while (await pipe.Input.ReadAsync() is var result && !result.IsCompleted)
            {
                await pipe.Output.WriteAsync(result.Buffer.First);
                pipe.Input.AdvanceTo(result.Buffer.End);
            }
        }
    }
}
