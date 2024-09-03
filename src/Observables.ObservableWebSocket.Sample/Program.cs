using Microsoft.AspNetCore.Builder;

var app = WebApplication.CreateBuilder(args).Build();
app.UseWebSockets();
app.Map(
	"",
	async (context) =>
	{
		using var serverSocket = await context.WebSockets.AcceptWebSocketAsync();
		using var observableServerSocket = new ObservableWebSocket(serverSocket);
		observableServerSocket.Received +=
			(object? sender, ObservableWebSocketData data) =>
				Console.WriteLine(System.Text.Encoding.UTF8.GetString(data.Bytes.Span));
		Console.CancelKeyPress +=
			async (object? sender, ConsoleCancelEventArgs e) =>
			{
				await serverSocket.CloseOutputAsync(
					System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
					statusDescription: null,
					cancellationToken: new()
				);
			};
		_ = observableServerSocket.ListenAsync();
		while (observableServerSocket.State == System.Net.WebSockets.WebSocketState.Open)
		{
			await observableServerSocket.SendAsync(
				System.Text.Encoding.UTF8.GetBytes("Hello World!"),
				System.Net.WebSockets.WebSocketMessageType.Text,
				endOfMessage: true,
				cancellationToken: new()
			);
			await Task.Delay(1000);
		}
	}
);

await app.StartAsync();
Console.CancelKeyPress += async (object? sender, ConsoleCancelEventArgs e) =>
{
	await app.StopAsync();
	Environment.Exit(0);
};

var clientSocket = new System.Net.WebSockets.ClientWebSocket();
await clientSocket.ConnectAsync(
	new Uri(app.Urls.First().Replace("http://", "ws://")),
	cancellationToken: new()
);
var observableClientSocket = new ObservableWebSocket(clientSocket);

observableClientSocket.Received +=
	(object? sender, ObservableWebSocketData data) =>
		Console.WriteLine(System.Text.Encoding.UTF8.GetString(data.Bytes.Span));

_ = observableClientSocket.ListenAsync();

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

await Task.Delay(Timeout.Infinite);
