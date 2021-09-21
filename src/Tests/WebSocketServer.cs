using System.IO.Pipelines;
using System.Net;
using System.Net.WebSockets;
using Xunit.Abstractions;

namespace Devlooped
{
    public record WebSocketServer(Uri Uri, Task RunTask, CancellationTokenSource Cancellation) : IAsyncDisposable, IDisposable
    {
        static int serverPort = 10000;

        public static WebSocketServer Create(Func<IWebSocketPipe, Task> behavior, WebSocketPipeOptions? options = null, ITestOutputHelper? output = null)
            => Create(behavior, null, options, output);

        public static WebSocketServer Create(Func<WebSocket, Task> behavior, WebSocketPipeOptions? options = null, ITestOutputHelper? output = null)
            => Create(null, behavior, options, output);

        static WebSocketServer Create(Func<IWebSocketPipe, Task>? pipeBehavior, Func<WebSocket, Task>? socketBehavior, WebSocketPipeOptions? options = null, ITestOutputHelper? output = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            var port = Interlocked.Increment(ref serverPort);

            // Only turn on output loggig when running tests in the IDE, for easier troubleshooting.
            if (output != null && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSAPPIDNAME")))
                builder.Logging.AddProvider(new LoggingProvider(output));

            var app = builder.Build();
            app.Urls.Add("http://localhost:" + port);

            app.UseWebSockets();

            var cts = new CancellationTokenSource();

            options ??= WebSocketPipeOptions.Default;

            app.Use(async (context, next) =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await next();
                }
                else
                {
                    using var websocket = await context.WebSockets.AcceptWebSocketAsync(
                        context.WebSockets.WebSocketRequestedProtocols.FirstOrDefault());

                    if (pipeBehavior != null)
                    {
                        using var pipe = WebSocketPipe.Create(websocket, options);
                        await Task.WhenAll(pipeBehavior(pipe), pipe.RunAsync(cts.Token));
                    }
                    else if (socketBehavior != null)
                    {
                        await socketBehavior(websocket);
                    }
                }
            });

            var task = app.RunAsync(cts.Token);
            return new WebSocketServer(new Uri("ws://localhost:" + port), task, cts);
        }

        public void Dispose()
        {
            Cancellation.Cancel();
            RunTask.Wait();
        }

        public async ValueTask DisposeAsync()
        {
            Cancellation.Cancel();
            await RunTask;
        }

        record LoggingProvider(ITestOutputHelper Output) : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName) => new OutputLogger(Output);
            public void Dispose() { }
            record OutputLogger(ITestOutputHelper Output) : ILogger
            {
                public IDisposable BeginScope<TState>(TState state) => NullDisposable.Default;
                public bool IsEnabled(LogLevel logLevel) => true;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                    Output.WriteLine($"{logLevel.ToString().Substring(0, 4)}: {formatter.Invoke(state, exception)}");
            }
        }

        class NullDisposable : IDisposable
        {
            public static IDisposable Default { get; } = new NullDisposable();
            NullDisposable() { }
            public void Dispose() { }
        }
    }
}
