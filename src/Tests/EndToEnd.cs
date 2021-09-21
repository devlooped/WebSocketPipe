using System.IO.Pipelines;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Devlooped
{
    public record EndToEnd(ITestOutputHelper Output)
    {
        [Fact]
        public async Task RunAsync()
        {
            await using var server = WebSocketServer.Create(Echo, null, Output);

            var client = new ClientWebSocket();

            await client.ConnectAsync(server.Uri, CancellationToken.None);
            using var pipe = WebSocketPipe.Create(client, closeWhenCompleted: true);

            var read = Task.Run(async () =>
            {
                var read = await pipe.Input.ReadAsync();
                Output.WriteLine("Client: " + Encoding.UTF8.GetString(read.Buffer));
                await pipe.CompleteAsync(WebSocketCloseStatus.NormalClosure, "Client Done");
            });

            var run = pipe.RunAsync();

            pipe.Output.Advance(Encoding.UTF8.GetBytes("hello", pipe.Output.GetSpan()));
            await pipe.Output.FlushAsync();

            //await pipe.Output.WriteAsync(Encoding.UTF8.GetBytes("hello").AsMemory());

            await read;
            await run;

            Assert.NotEqual(WebSocketState.Open, pipe.State);
            Assert.Equal("Client Done", pipe.CloseStatusDescription);
        }

        async Task Echo(IDuplexPipe pipe)
        {
            while (await pipe.Input.ReadAsync() is var result && !result.IsCompleted)
            {
                Output.WriteLine($"Echoing: {Encoding.UTF8.GetString(result.Buffer)}");
                // Just assume we get a single-segment entry, for simplicity
                await pipe.Output.WriteAsync(result.Buffer.First);
                pipe.Input.AdvanceTo(result.Buffer.End);
            }
            Output.WriteLine($"Server: Done.");
        }
    }
}