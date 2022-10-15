![Icon](https://raw.githubusercontent.com/devlooped/WebSocketPipe/main/assets/img/icon.png) WebSocketPipe
============

High-performance System.IO.Pipelines API adapter for System.Net.WebSockets

[![Version](https://img.shields.io/nuget/vpre/WebSocketPipe.svg?color=royalblue)](https://www.nuget.org/packages/WebSocketPipe)
[![Downloads](https://img.shields.io/nuget/dt/WebSocketPipe.svg?color=green)](https://www.nuget.org/packages/WebSocketPipe)
[![License](https://img.shields.io/github/license/devlooped/WebSocketPipe.svg?color=blue)](https://github.com/devlooped/WebSocketPipe/blob/main/license.txt)
[![Build](https://github.com/devlooped/WebSocketPipe/workflows/build/badge.svg?branch=main)](https://github.com/devlooped/WebSocketPipe/actions)

<!-- #content -->
# Usage

```csharp
using Devlooped.Net;

var client = new ClientWebSocket();
await client.ConnectAsync(serverUri, CancellationToken.None);

using IWebSocketPipe pipe = WebSocketPipe.Create(client, closeWhenCompleted: true);

// Start the pipe before hooking up the processing
var run = pipe.RunAsync();
```

The `IWebSocketPipe` interface extends [IDuplexPipe](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.iduplexpipe?view=dotnet-plat-ext-5.0), 
exposing `Input` and `Output` properties that can be used to 
read incoming messages and write outgoing ones.

For example, to read incoming data and write it to the console, 
we could write the following code:

```csharp
await ReadIncoming(pipe.Input);

async Task ReadIncoming(PipeReader reader)
{
    while (await reader.ReadAsync() is var result && !result.IsCompleted)
    {
        Console.WriteLine($"Received: {Encoding.UTF8.GetString(result.Buffer)}");
        reader.AdvanceTo(result.Buffer.End);
    }
    Console.WriteLine($"Done reading.");
}
```

Similarly, to write to the underlying websocket the input 
entered in the console, we use code like the following: 

```csharp
await SendOutgoing(pipe.Output);

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

If we wanted to simultaneously read and write and wait for 
completion of both operations, we could just wait for both 
tasks:

```csharp
// Wait for completion of processing code
await Task.WhenAny(
    ReadIncoming(pipe.Input),
    SendOutgoing(pipe.Output));
```

Note that completing the `PipeWriter` automatically causes the 
reader to reveive a completed result and exit the loop. In addition, 
the overall `IWebSocketPipe.RunAsync` task will also run to completion. 


The `IWebSocketPipe` takes care of gracefully closing the connection 
when the input or output are completed, if `closeWhenCompleted` is set 
to true when creating it. 

Alternatively, it's also possible to complete the entire pipe explicitly, 
while setting an optional socket close status and status description for 
the server to act on:

```csharp
await pipe.CompleteAsync(WebSocketCloseStatus.NormalClosure, "Done processing");
```

Specifying a close status will always close the underlying socket.

The `WebSocketPipe` can also be used on the server. The following example is basically 
taken from the documentation on [WebSockets in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-5.0#configure-the-middleware) 
and adapted to use a `WebSocketPipe` to echo messages to the client:

```csharp
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var websocket = await context.WebSockets.AcceptWebSocketAsync();
            using var pipe = WebSocketPipe.Create(websocket, true);
            await Task.WhenAll(Echo(pipe), pipe.RunAsync(context.RequestAborted));
        }
        else
        {
            context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
        }
    }
    else
    {
        await next();
    }
});
```

The sample `Echo` method is simply:

```csharp
async Task Echo(IDuplexPipe pipe)
{
    while (await pipe.Input.ReadAsync() is var result && !result.IsCompleted)
    {
        // Just assume we get a single-segment entry, for simplicity
        await pipe.Output.WriteAsync(result.Buffer.First);
        pipe.Input.AdvanceTo(result.Buffer.End);
    }
}
```
<!-- #content -->

# Dogfooding

[![CI Version](https://img.shields.io/endpoint?url=https://shields.kzu.io/vpre/WebSocketPipe/main&label=nuget.ci&color=brightgreen)](https://pkg.kzu.io/index.json)
[![Build](https://github.com/devlooped/WebSocketPipe/workflows/build/badge.svg?branch=main)](https://github.com/devlooped/WebSocketPipe/actions)

We also produce CI packages from branches and pull requests so you can dogfood builds as quickly as they are produced. 

The CI feed is `https://pkg.kzu.io/index.json`. 

The versioning scheme for packages is:

- PR builds: *42.42.42-pr*`[NUMBER]`
- Branch builds: *42.42.42-*`[BRANCH]`.`[COMMITS]`

<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->
# Sponsors 

<!-- sponsors.md -->
[![Clarius Org](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/clarius.png "Clarius Org")](https://github.com/clarius)
[![Christian Findlay](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/MelbourneDeveloper.png "Christian Findlay")](https://github.com/MelbourneDeveloper)
[![C. Augusto Proiete](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/augustoproiete.png "C. Augusto Proiete")](https://github.com/augustoproiete)
[![Kirill Osenkov](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/KirillOsenkov.png "Kirill Osenkov")](https://github.com/KirillOsenkov)
[![MFB Technologies, Inc.](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/MFB-Technologies-Inc.png "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![SandRock](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/sandrock.png "SandRock")](https://github.com/sandrock)
[![Andy Gocke](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/agocke.png "Andy Gocke")](https://github.com/agocke)
[![Shahzad Huq](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/shahzadhuq.png "Shahzad Huq")](https://github.com/shahzadhuq)


<!-- sponsors.md -->

[![Sponsor this project](https://raw.githubusercontent.com/devlooped/sponsors/main/sponsor.png "Sponsor this project")](https://github.com/sponsors/devlooped)
&nbsp;

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->
