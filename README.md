# ReillyDigital.Observables.ObservableWebSocket

An observable WebSocket library for .NET for creating WebSockets that have subscribable events.

## Usage

### Server Example

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
		await serverSocket.CloseOutputAsync(
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
while (serverSocket.State == System.Net.WebSockets.WebSocketState.Open)
{
	await serverSocket.SendAsync(
		System.Text.Encoding.UTF8.GetBytes("Hello World!"),
		System.Net.WebSockets.WebSocketMessageType.Text,
		endOfMessage: true,
		cancellationToken: new()
	);
	await Task.Delay(1000);
}
```

## Client Example

Given a client that is requesting a WebSocket connection:
```csharp
var clientSocket = new System.Net.WebSockets.ClientWebSocket();
await clientSocket.ConnectAsync(
	new Uri("ws://localhost:5000"), cancellationToken: new()
);
```

Get an ObservableWebSocket wrapper:
```csharp
var observableClientSocket = new ObservableWebSocket(clientSocket);
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
while (observableClientSocket.State == System.Net.WebSockets.WebSocketState.Open)
{
	await observableClientSocket.SendAsync(
		System.Text.Encoding.UTF8.GetBytes("Hello back!"),
		System.Net.WebSockets.WebSocketMessageType.Text,
		endOfMessage: true,
		cancellationToken: new()
	);
	await Task.Delay(1000);
}
```

## Links

Sample Project:
https://gitlab.com/reilly-digital/dotnet/observables.observablewebsocket/-/tree/main/src/Observables.ObservableWebSocket.Sample

NuGet:
https://www.nuget.org/packages/ReillyDigital.Observables.ObservableWebSocket
