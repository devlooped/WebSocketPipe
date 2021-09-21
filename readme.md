![Icon](https://raw.githubusercontent.com/devlooped/WebSocketPipe/main/assets/img/icon.png) WebSocketPipe
============

[![Version](https://img.shields.io/nuget/vpre/WebSocketPipe.svg?color=royalblue)](https://www.nuget.org/packages/WebSocketPipe)
[![Downloads](https://img.shields.io/nuget/dt/WebSocketPipe.svg?color=green)](https://www.nuget.org/packages/WebSocketPipe)
[![License](https://img.shields.io/github/license/devlooped/WebSocketPipe.svg?color=blue)](https://github.com/devlooped/WebSocketPipe/blob/main/license.txt)
[![Build](https://github.com/devlooped/WebSocketPipe/workflows/build/badge.svg?branch=main)](https://github.com/devlooped/WebSocketPipe/actions)

A System.IO.Pipelines adapter API over System.Net.WebSockets

# Usage

```csharp
using Devlooped;

var client = new ClientWebSocket();
await client.ConnectAsync(serverUri, CancellationToken.None);

using IWebSocketPipe pipe = WebSocketPipe.Create(client, closeWhenCompleted: true);

// Start the pipe before hooking up the processing
var run = pipe.RunAsync();

// Wait for completion of processing code
await Task.WhenAny(
    ReadIncoming(pipe.Input),
    SendOutgoing(pipe.Output));

// When the processing completes, the overall pipe run will also complete
await run;

// Reads incoming data and writes to the console
async Task ReadIncoming(PipeReader reader)
{
    while (await reader.ReadAsync() is var result && !result.IsCompleted)
    {
        Console.WriteLine($"Received: {Encoding.UTF8.GetString(result.Buffer)}");
        reader.AdvanceTo(result.Buffer.Start, result.Buffer.End);
    }
    Console.WriteLine($"Done reading.");
}

// Reads console input and writes to pipe until an empty line is entered
async Task SendOutgoing(PipeWriter writer)
{
    while (Console.ReadLine() is var line && line?.Length > 0)
    {
        Encoding.UTF8.GetBytes(line, writer);
    }
    await writer.CompleteAsync();
    Console.WriteLine($"Done writing.");
}
```

