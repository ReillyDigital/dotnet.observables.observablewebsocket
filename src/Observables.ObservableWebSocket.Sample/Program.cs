using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

System.Net.WebSockets.WebSocket? serverSocket = null;
var app = WebApplication.CreateBuilder(args).Build();
app.UseWebSockets();
app.Map(
	"",
	async (context) =>
		serverSocket = await context.WebSockets.AcceptWebSocketAsync()
);
app.Start();

var observableWebSocket =
	new ObservableWebSocket(app.Urls.First().Replace("http://", "ws://"));

observableWebSocket.Received +=
	(object? sender, ObservableWebSocketData data) =>
		Console.WriteLine(System.Text.Encoding.UTF8.GetString(data.Bytes.Span));
observableWebSocket.ListenAsync();
Task.Delay(1000).Wait();

serverSocket?
	.SendAsync(
		System.Text.Encoding.UTF8.GetBytes("Hello World!"),
		System.Net.WebSockets.WebSocketMessageType.Text,
		true,
		new CancellationToken()
	)
	.Wait();
Task.Delay(1000).Wait();
