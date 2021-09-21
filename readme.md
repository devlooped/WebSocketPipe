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

var read = Task.Run(async () =>
{
    var read = await pipe.Input.ReadAsync();
    Output.WriteLine("Client: " + Encoding.UTF8.GetString(read.Buffer));
    await pipe.CompleteAsync(WebSocketCloseStatus.NormalClosure, "Client Done");
});

var run = pipe.RunAsync();

await pipe.Output.WriteAsync(Encoding.UTF8.GetBytes("hello").AsMemory());



```

