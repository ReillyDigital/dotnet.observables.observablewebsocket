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
				await observableServerSocket.CloseOutputNormalAsync(
					cancellationToken: new()
				);
			};
		_ = observableServerSocket.ListenAsync();
		while (
			observableServerSocket.State == System.Net.WebSockets.WebSocketState.Open
		)
		{
			await observableServerSocket.SendFullMessageTextAsync("Hello World!");
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

var observableClientSocket = new ObservableWebSocket(
	app.Urls.First().Replace("http://", "ws://")
);

observableClientSocket.Received +=
	(object? sender, ObservableWebSocketData data) =>
		Console.WriteLine(System.Text.Encoding.UTF8.GetString(data.Bytes.Span));

_ = observableClientSocket.ListenAsync();

while (
	observableClientSocket.State == System.Net.WebSockets.WebSocketState.Open
)
{
	await observableClientSocket.SendFullMessageTextAsync("Hello back!");
	await Task.Delay(1000);
}

await Task.Delay(Timeout.Infinite);
