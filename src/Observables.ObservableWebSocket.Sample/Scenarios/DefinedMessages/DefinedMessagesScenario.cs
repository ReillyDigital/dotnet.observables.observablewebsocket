namespace ReillyDigital.Observables.ObservableWebSocket.Sample;

using Microsoft.AspNetCore.Builder;

using ReillyDigital.Observables;

public static class DefinedMessagesScenario
{
	public static void Run(params string[] args)
	{
		// For tracking when example is exited upon cancel keypress:
		var taskCompletionSource = new TaskCompletionSource();

		// Given a server that is accepting WebSocket connections:
		var app = WebApplication.CreateBuilder(args).Build();
		app.UseWebSockets();
		app.Map(
			"",
			async (context) =>
			{
				// Get a handle on the underlying accepted WebSocket connection:
				using var serverSocket = await context.WebSockets.AcceptWebSocketAsync();

				// Get an ObservableWebSocket wrapper for the message type to handle:
				using var observableServerSocket = new ObservableWebSocket<GreetingMessage>(serverSocket);

				// Observe the Received event to do stuff whenever a message of the specified type is received:
				observableServerSocket.Received +=
					(object? sender, GreetingMessage message) =>
						Console.WriteLine($"{message.Greeting} {string.Join(", ", message.Recipients)}");

				// Send an output closure message for good measure when the server exits:
				async void OnCancelKeyPressAsync(object? sender, ConsoleCancelEventArgs e)
				{
					await observableServerSocket.CloseOutputNormalAsync(cancellationToken: new());
					Console.CancelKeyPress -= OnCancelKeyPressAsync;
				}
				Console.CancelKeyPress += OnCancelKeyPressAsync;

				// Listen in a background thread for incoming messages of the specified type that would trigger the received
				// event:
				_ = observableServerSocket.ListenAsync();

				// Send an indefinite amount of messages of the specified type while the socket connection is open:
				while (observableServerSocket.State == System.Net.WebSockets.WebSocketState.Open)
				{
					await observableServerSocket.SendAsync(new GreetingMessage("Hello", ["Alice", "Bob", "World"]));
					await Task.Delay(1000);
				}
			}
		);

		// Start the server:
		app.StartAsync().Wait();

		// Exit the example upon cancel keypress:
		async void OnCancelKeyPressAsync(object? sender, ConsoleCancelEventArgs e)
		{
			await app.StopAsync();
			taskCompletionSource.SetResult();
			Console.CancelKeyPress -= OnCancelKeyPressAsync;
		}
		Console.CancelKeyPress += OnCancelKeyPressAsync;

		// Get an ObservableWebSocket wrapper for a client connection:
		var observableClientSocket =
			new ObservableWebSocket<GreetingMessage>(app.Urls.First().Replace("http://", "ws://"));

		// Observe the Received event to do stuff whenever a message of the specified type is received:
		observableClientSocket.Received +=
			(object? sender, GreetingMessage message) =>
				Console.WriteLine($"{message.Greeting} {string.Join(", ", message.Recipients)}");

		// Listen in a background thread for incoming messages of the specified type that would trigger the received
		// event:
		_ = observableClientSocket.ListenAsync();

		// Send an indefinite amount of messages of the specified type while the socket connection is open:
		while (observableClientSocket.State == System.Net.WebSockets.WebSocketState.Open)
		{
			observableClientSocket.SendAsync(
				new GreetingMessage("Hello back", ["Plato", "Aristotle", "Server"])
			).AsTask().Wait();
			Task.Delay(1000).Wait();
		}

		// Wait for the example to be exited via cancel keypress:
		taskCompletionSource.Task.Wait();
	}

	// Define a message type for the messages to be sent and received over a WebSocket connection:
	public record GreetingMessage(string Greeting, string[] Recipients);
}
