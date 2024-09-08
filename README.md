# ReillyDigital.Observables.ObservableWebSocket

An observable WebSocket library for .NET for creating WebSockets that have subscribable events.

## Usage

### Standard Message Example (Server Usage)

Given a server that is accepting WebSocket connections:
```csharp
using Microsoft.AspNetCore.Builder;

var app = WebApplication.CreateBuilder(args).Build();
app.UseWebSockets();

app.Map(
	"",
	async (context) =>
	{
		using var serverSocket = await context.WebSockets.AcceptWebSocketAsync();
		// Do stuff...
	}
);

await app.StartAsync();
Console.CancelKeyPress += async (object? sender, ConsoleCancelEventArgs e) =>
{
	await app.StopAsync();
	Environment.Exit(0);
};
```

The above route can utilize an `ObservableWebSocket` to send and receive messages for an indefinite period of time.

Note: The below logic would stil exist inside the `app.Map(...)` handler.

Get a handle on the underlying accepted WebSocket connection:
```csharp
using var serverSocket = await context.WebSockets.AcceptWebSocketAsync();
```

Get an ObservableWebSocket wrapper:
```csharp
using var observableServerSocket = new ObservableWebSocket(serverSocket);
```

Observe the Received event to do stuff whenever a message is received:
```csharp
observableServerSocket.Received +=
	(object? sender, ObservableWebSocketData data) =>
		Console.WriteLine(System.Text.Encoding.UTF8.GetString(data.Bytes.Span));
```

Send an output closure message for good measure when the server exits:
```csharp
Console.CancelKeyPress +=
	async (object? sender, ConsoleCancelEventArgs e) =>
	{
		await observableServerSocket.CloseOutputAsync(
			System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
			statusDescription: null,
			cancellationToken: new()
		);
	};
```

Listen in a background thread for incoming messages that would trigger the received event:
```csharp
_ = observableServerSocket.ListenAsync();
```

Send an indefinite amount of messages while the socket connection is open:
```csharp
while (
	observableServerSocket.State == System.Net.WebSockets.WebSocketState.Open
)
{
	await observableServerSocket.SendFullMessageTextAsync("Hello World!");
	await Task.Delay(1000);
}
```

### Standard Message Example (Client Usage)

Get an ObservableWebSocket wrapper:
```csharp
var observableClientSocket = new ObservableWebSocket("ws://localhost:5000");
```

Observe the Received event to do stuff whenever a message is received:
```csharp
observableClientSocket.Received +=
	(object? sender, ObservableWebSocketData data) =>
		Console.WriteLine(System.Text.Encoding.UTF8.GetString(data.Bytes.Span));
```

Listen in a background thread for incoming messages that would trigger the received event:
```csharp
_ = observableClientSocket.ListenAsync();
```

Send an indefinite amount of messages while the socket connection is open:
```csharp
while (
	observableClientSocket.State == System.Net.WebSockets.WebSocketState.Open
)
{
	await observableClientSocket.SendFullMessageTextAsync("Hello back!");
	await Task.Delay(1000);
}
```

### Defined Message Example (Server Usage)

Define a message type for the messages to be sent and received over a WebSocket connection:
```csharp
public record GreetingMessage(string Greeting, string[] Recipients);
```

Given a server that is accepting WebSocket connections:
```csharp
using Microsoft.AspNetCore.Builder;

var app = WebApplication.CreateBuilder(args).Build();
app.UseWebSockets();

app.Map(
	"",
	async (context) =>
	{
		using var serverSocket = await context.WebSockets.AcceptWebSocketAsync();
		// Do stuff...
	}
);

await app.StartAsync();
Console.CancelKeyPress += async (object? sender, ConsoleCancelEventArgs e) =>
{
	await app.StopAsync();
	Environment.Exit(0);
};
```

The above route can utilize an `ObservableWebSocket` to send and receive messages of the specified type for an indefinite period of time.

Note: The below logic would stil exist inside the `app.Map(...)` handler.

Get a handle on the underlying accepted WebSocket connection:
```csharp
using var serverSocket = await context.WebSockets.AcceptWebSocketAsync();
```

Get an ObservableWebSocket wrapper for the message type to handle:
```csharp
using var observableServerSocket =
	new ObservableWebSocket<GreetingMessage>(serverSocket);
```

Observe the Received event to do stuff whenever a message of the specified type is received:
```csharp
observableServerSocket.Received +=
	(object? sender, GreetingMessage message) =>
		Console.WriteLine(
			$"{message.Greeting} {string.Join(", ", message.Recipients)}"
		);
```

Send an output closure message for good measure when the server exits:
```csharp
Console.CancelKeyPress +=
	async (object? sender, ConsoleCancelEventArgs e) =>
	{
		await observableServerSocket.CloseOutputAsync(
			System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
			statusDescription: null,
			cancellationToken: new()
		);
	};
```

Listen in a background thread for incoming messages of the specified type that would trigger the received event:
```csharp
_ = observableServerSocket.ListenAsync();
```

Send an indefinite amount of messages of the specified type while the socket connection is open:
```csharp
while (
	observableServerSocket.State == System.Net.WebSockets.WebSocketState.Open
)
{
	await observableServerSocket.SendAsync(
		new GreetingMessage("Hello", ["Alice", "Bob", "World"])
	);
	await Task.Delay(1000);
}
```

### Defined Message Example (Client Usage)

Define a message type for the messages to be sent and received over a WebSocket connection:
```csharp
public record GreetingMessage(string Greeting, string[] Recipients);
```

Get an ObservableWebSocket wrapper:
```csharp
var observableClientSocket =
	new ObservableWebSocket<GreetingMessage>("ws://localhost:5000");
```

Observe the Received event to do stuff whenever a message of the specified type is received:
```csharp
observableClientSocket.Received +=
	(object? sender, GreetingMessage message) =>
		Console.WriteLine(
			$"{message.Greeting} {string.Join(", ", message.Recipients)}"
		);
```

Listen in a background thread for incoming messages of the specified type that would trigger the received event:
```csharp
_ = observableClientSocket.ListenAsync();
```

Send an indefinite amount of messages of the specified type while the socket connection is open:
```csharp
while (
	observableClientSocket.State == System.Net.WebSockets.WebSocketState.Open
)
{
	await observableClientSocket.SendAsync(
		new GreetingMessage("Hello back", ["Plato", "Aristotle", "Server"])
	)
	await Task.Delay(1000);
}
```
## Links

Sample Project:
https://gitlab.com/reilly-digital/dotnet/observables.observablewebsocket/-/tree/main/src/Observables.ObservableWebSocket.Sample

NuGet:
https://www.nuget.org/packages/ReillyDigital.Observables.ObservableWebSocket
