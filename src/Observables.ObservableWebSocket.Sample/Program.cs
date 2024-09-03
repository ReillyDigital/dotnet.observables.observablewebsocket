using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

var app = WebApplication.CreateBuilder(args).Build();
app.UseWebSockets();
app.Map(
	"",
	async (context) =>
	{
		using var socket = await context.WebSockets.AcceptWebSocketAsync();
		await socket.SendAsync(
			System.Text.Encoding.UTF8.GetBytes("Hello World!"),
			System.Net.WebSockets.WebSocketMessageType.Text,
			endOfMessage: true,
			cancellationToken: new()
		);
		await Task.Delay(5000);
	}
);
app.Start();

//var observableWebSocket =
//	new ObservableWebSocket(app.Urls.First().Replace("http://", "ws://"));
//observableWebSocket.Received +=
//	(object? sender, ObservableWebSocketData data) =>
//		Console.WriteLine(System.Text.Encoding.UTF8.GetString(data.Bytes.Span));
//observableWebSocket.ListenAsync();

await Task.Run(
	async () =>
	{
		using var socket = new System.Net.WebSockets.ClientWebSocket();
		await socket.ConnectAsync(new Uri(app.Urls.First().Replace("http://", "ws://")), new());
		var buffer = new Memory<byte>(new byte[2048]);
		await socket.ReceiveAsync(buffer, new());
	}
);
