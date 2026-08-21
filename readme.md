![Icon](https://raw.githubusercontent.com/devlooped/WebSocketPipe/main/assets/img/icon.png) WebSocketPipe
============

High-performance System.IO.Pipelines API adapter for System.Net.WebSockets

[![Version](https://img.shields.io/nuget/vpre/WebSocketPipe.svg?color=royalblue)](https://www.nuget.org/packages/WebSocketPipe)
[![Downloads](https://img.shields.io/nuget/dt/WebSocketPipe.svg?color=green)](https://www.nuget.org/packages/WebSocketPipe)
[![License](https://img.shields.io/github/license/devlooped/WebSocketPipe.svg?color=blue)](https://github.com/devlooped/WebSocketPipe/blob/main/license.txt)
[![Build](https://github.com/devlooped/WebSocketPipe/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/devlooped/WebSocketPipe/actions)

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

[![CI Version](https://img.shields.io/endpoint?url=https://shields.kzu.app/vpre/WebSocketPipe/main&label=nuget.ci&color=brightgreen)](https://pkg.kzu.app/index.json)
[![Build](https://github.com/devlooped/WebSocketPipe/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/devlooped/WebSocketPipe/actions)

We also produce CI packages from branches and pull requests so you can dogfood builds as quickly as they are produced. 

The CI feed is `https://pkg.kzu.app/index.json`. 

The versioning scheme for packages is:

- PR builds: *42.42.42-pr*`[NUMBER]`
- Branch builds: *42.42.42-*`[BRANCH]`.`[COMMITS]`

<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->
# Sponsors 

<!-- sponsors.md -->
[![Clarius Org](https://avatars.githubusercontent.com/u/71888636?v=4&s=39 "Clarius Org")](https://github.com/clarius)
[![MFB Technologies, Inc.](https://avatars.githubusercontent.com/u/87181630?v=4&s=39 "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![SandRock](https://avatars.githubusercontent.com/u/321868?u=99e50a714276c43ae820632f1da88cb71632ec97&v=4&s=39 "SandRock")](https://github.com/sandrock)
[![DRIVE.NET, Inc.](https://avatars.githubusercontent.com/u/15047123?v=4&s=39 "DRIVE.NET, Inc.")](https://github.com/drivenet)
[![Keith Pickford](https://avatars.githubusercontent.com/u/16598898?u=64416b80caf7092a885f60bb31612270bffc9598&v=4&s=39 "Keith Pickford")](https://github.com/Keflon)
[![Thomas Bolon](https://avatars.githubusercontent.com/u/127185?u=7f50babfc888675e37feb80851a4e9708f573386&v=4&s=39 "Thomas Bolon")](https://github.com/tbolon)
[![Reuben Swartz](https://avatars.githubusercontent.com/u/724704?u=2076fe336f9f6ad678009f1595cbea434b0c5a41&v=4&s=39 "Reuben Swartz")](https://github.com/rbnswartz)
[![Jacob Foshee](https://avatars.githubusercontent.com/u/480334?v=4&s=39 "Jacob Foshee")](https://github.com/jfoshee)
[![](https://avatars.githubusercontent.com/u/33566379?u=bf62e2b46435a267fa246a64537870fd2449410f&v=4&s=39 "")](https://github.com/Mrxx99)
[![Eric Johnson](https://avatars.githubusercontent.com/u/26369281?u=41b560c2bc493149b32d384b960e0948c78767ab&v=4&s=39 "Eric Johnson")](https://github.com/eajhnsn1)
[![Jonathan ](https://avatars.githubusercontent.com/u/5510103?u=98dcfbef3f32de629d30f1f418a095bf09e14891&v=4&s=39 "Jonathan ")](https://github.com/Jonathan-Hickey)
[![Ken Bonny](https://avatars.githubusercontent.com/u/6417376?u=569af445b6f387917029ffb5129e9cf9f6f68421&v=4&s=39 "Ken Bonny")](https://github.com/KenBonny)
[![Simon Cropp](https://avatars.githubusercontent.com/u/122666?v=4&s=39 "Simon Cropp")](https://github.com/SimonCropp)
[![agileworks-eu](https://avatars.githubusercontent.com/u/5989304?v=4&s=39 "agileworks-eu")](https://github.com/agileworks-eu)
[![Zheyu Shen](https://avatars.githubusercontent.com/u/4067473?v=4&s=39 "Zheyu Shen")](https://github.com/arsdragonfly)
[![Vezel](https://avatars.githubusercontent.com/u/87844133?v=4&s=39 "Vezel")](https://github.com/vezel-dev)
[![ChilliCream](https://avatars.githubusercontent.com/u/16239022?v=4&s=39 "ChilliCream")](https://github.com/ChilliCream)
[![4OTC](https://avatars.githubusercontent.com/u/68428092?v=4&s=39 "4OTC")](https://github.com/4OTC)
[![domischell](https://avatars.githubusercontent.com/u/66068846?u=0a5c5e2e7d90f15ea657bc660f175605935c5bea&v=4&s=39 "domischell")](https://github.com/DominicSchell)
[![Adrian Alonso](https://avatars.githubusercontent.com/u/2027083?u=129cf516d99f5cb2fd0f4a0787a069f3446b7522&v=4&s=39 "Adrian Alonso")](https://github.com/adalon)
[![torutek](https://avatars.githubusercontent.com/u/33917059?v=4&s=39 "torutek")](https://github.com/torutek)
[![Ryan McCaffery](https://avatars.githubusercontent.com/u/16667079?u=c0daa64bb5c1b572130e05ae2b6f609ecc912d4d&v=4&s=39 "Ryan McCaffery")](https://github.com/mccaffers)
[![Seika Logiciel](https://avatars.githubusercontent.com/u/2564602?v=4&s=39 "Seika Logiciel")](https://github.com/SeikaLogiciel)
[![Andrew Grant](https://avatars.githubusercontent.com/devlooped-user?s=39 "Andrew Grant")](https://github.com/wizardness)
[![eska-gmbh](https://avatars.githubusercontent.com/devlooped-team?s=39 "eska-gmbh")](https://github.com/eska-gmbh)
[![Geodata AS](https://avatars.githubusercontent.com/u/5946299?v=4&s=39 "Geodata AS")](https://github.com/geodata-no)
[![Jiri Slachta](https://avatars.githubusercontent.com/u/6891947?u=802cfeb13b070d04c53269fc662b0d58963480dd&v=4&s=39 "Jiri Slachta")](https://github.com/jslachta)


<!-- sponsors.md -->
[![Sponsor this project](https://avatars.githubusercontent.com/devlooped-sponsor?s=118 "Sponsor this project")](https://github.com/sponsors/devlooped)

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->
