namespace ReillyDigital.Observables;

using System.Net.WebSockets;

/// <summary>
/// Observable wrapper for a websocket connection. Subscribable events are provided for when data is received or sent,
/// as well as events for when the connection is aborted, closed or disposed.
/// </summary>
public class ObservableWebSocket : IDisposable
{
	/// <summary>
	/// An event triggered when the websocket connection is aborted.
	/// </summary>
	public event EventHandler<IVoid>? Aborted;

	/// <summary>
	/// An event triggered when the websocket connection is closed.
	/// </summary>
	public event EventHandler<IVoid>? Closed;

	/// <summary>
	/// An event triggered when the websocket connection is disposed.
	/// </summary>
	public event EventHandler<IVoid>? Disposed;

	/// <summary>
	/// An event triggered when an output closure message is sent over the websocket connection.
	/// </summary>
	public event EventHandler<IVoid>? OutputClosed;

	/// <summary>
	/// An event triggered when data is received over the websocket connection.
	/// </summary>
	public event EventHandler<IOption<ObservableWebSocketData>>? Received;

	/// <summary>
	/// An event triggered when data is sent over the websocket connection.
	/// </summary>
	public event EventHandler<IOption<ObservableWebSocketData>>? Sent;

	/// <summary>
	/// Indicates the reason why the remote endpoint initiated the close handshake.
	/// </summary>
	public WebSocketCloseStatus? CloseStatus => WebSocket.CloseStatus;

	/// <summary>
	/// Allows the remote endpoint to describe the reason why the connection was closed.
	/// </summary>
	public string? CloseStatusDescription => WebSocket.CloseStatusDescription;

	/// <summary>
	/// The mode for how the <see cref="ObservableWebSocket" /> will be observed.
	/// </summary>
	public ObservableWebSocketMode Mode { get; }

	/// <summary>
	/// Returns the current state of the WebSocket connection.
	/// </summary>
	public WebSocketState State => WebSocket.State;

	/// <summary>
	/// Gets the subprotocol that was negotiated during the opening handshake.
	/// </summary>
	public string? SubProtocol => WebSocket.SubProtocol;

	/// <summary>
	/// A cancellation token for aborting the websocket connection.
	/// </summary>
	private CancellationToken CancellationToken { get; } = new();

	/// <summary>
	/// A task completion source for when the websocket connection is no longer expecting messages.
	/// </summary>
	private TaskCompletionSource TaskCompletionSource { get; } = new();

	/// <summary>
	/// The wrapped web socket connection.
	/// </summary>
	private WebSocket WebSocket { get; }

	/// <summary>
	/// Constructor for this class given an existing websocket connection to wrap.
	/// </summary>
	/// <param name="webSocket">The websocket connection to be wrapped.</param>
	/// <param name="mode">The mode for how the <see cref="ObservableWebSocket" /> will be observed.</param>
	/// <param name="cancellationToken">A cancellation token for aborting the websocket connection.</param>
	public ObservableWebSocket(
		WebSocket webSocket,
		ObservableWebSocketMode mode = ObservableWebSocketMode.ObserveFullMessages,
		CancellationToken cancellationToken = default
	)
	{
		if (webSocket.State != WebSocketState.Open)
		{
			throw new TypeInitializationException(
				typeof(ObservableWebSocket).FullName, new ArgumentException($"WebSocket must be open.", nameof(webSocket))
			);
		}
		(Mode, WebSocket) = (mode, webSocket);
		cancellationToken.Register(TaskCompletionSource.SetCanceled);
		InitializeWebSocketListenLoopAsync();
	}

	/// <summary>
	/// Constructor for this class given a URI to use for a new websocket connection to wrap.
	/// </summary>
	/// <param name="uri">The URI for a new websocket connection to wrap.</param>
	/// <param name="mode">The mode for how the <see cref="ObservableWebSocket" /> will be observed.</param>
	/// <param name="cancellationToken">A cancellation token for aborting the websocket connection.</param>
	public ObservableWebSocket(
		string uri,
		ObservableWebSocketMode mode = ObservableWebSocketMode.ObserveFullMessages,
		CancellationToken cancellationToken = default
	) : this(new Uri(uri), mode: mode, cancellationToken: cancellationToken) { }

	/// <summary>
	/// Constructor for this class given a URI to use for a new websocket connection to wrap.
	/// </summary>
	/// <param name="uri">The URI for a new websocket connection to wrap.</param>
	/// <param name="mode">The mode for how the <see cref="ObservableWebSocket" /> will be observed.</param>
	/// <param name="cancellationToken">A cancellation token for aborting the websocket connection.</param>
	public ObservableWebSocket(
		Uri uri,
		ObservableWebSocketMode mode = ObservableWebSocketMode.ObserveFullMessages,
		CancellationToken cancellationToken = default
	)
	{
		var webSocket = new ClientWebSocket();
		webSocket.ConnectAsync(uri, new()).Wait(cancellationToken);
		(Mode, WebSocket) = (mode, webSocket);
		cancellationToken.Register(TaskCompletionSource.SetCanceled);
		InitializeWebSocketListenLoopAsync();
	}

	/// <summary>
	/// Aborts the wrapped websocket connection and invokes the <see cref="Aborted" /> event.
	/// </summary>
	public void Abort()
	{
		WebSocket.Abort();
		Aborted?.Invoke(this, Void());
	}

	/// <summary>
	/// Closes the wrapped websocket connection and invokes the <see cref="Closed" /> event.
	/// </summary>
	/// <param name="status">The status of the closure.</param>
	/// <param name="description">The description of the closure.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public async ValueTask CloseAsync(WebSocketCloseStatus status, string? description)
	{
		await WebSocket.CloseAsync(status, description, cancellationToken: CancellationToken);
		Closed?.Invoke(this, Void());
	}

	/// <summary>
	/// Sends an output closure message over the websocket connection and invokes the <see cref="OutputClosed" /> event.
	/// </summary>
	/// <param name="status">The status of the closure.</param>
	/// <param name="description">The description of the closure.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public async ValueTask CloseOutputAsync(
		WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string? description = null
	)
	{
		await WebSocket.CloseOutputAsync(status, description, cancellationToken: CancellationToken);
		OutputClosed?.Invoke(this, Void());
	}

	/// <summary>
	/// Disposes the wrapped websocket connection and invokes the <see cref="Disposed" /> event.
	/// </summary>
	public void Dispose()
	{
		WebSocket.Dispose();
		Disposed?.Invoke(this, Void());
	}

	/// <summary>
	/// Sends a chunk of data over the websocket connection.
	/// </summary>
	/// <param name="buffer">The buffer to be sent over the connection.</param>
	/// <param name="messageType">Indicates whether the application is sending a binary or text message.</param>
	/// <param name="endOfMessage">Indicates whether the data in "buffer" is the last part of a message.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public async ValueTask SendAsync(
		ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage
	)
	{
		await WebSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken: CancellationToken);
		Sent?.Invoke(this, Some(new ObservableWebSocketData(buffer, messageType, endOfMessage)));
	}

	/// <summary>
	/// Sends a full message over the websocket connection.
	/// </summary>
	/// <param name="message">The message bytes to send over the websocket connection.</param>
	/// <param name="messageType">The type of websocket message.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public async ValueTask SendFullMessageAsync(
		ReadOnlyMemory<byte> message, WebSocketMessageType messageType = WebSocketMessageType.Text
	)
	{
		await SendMessageAsChunksAsync(message, messageType: messageType);
		Sent?.Invoke(this, Some(new ObservableWebSocketData(message, messageType, true)));
	}

	/// <summary>
	/// Initialized a continuous listen loop for websocket messages.
	/// </summary>
	private async void InitializeWebSocketListenLoopAsync()
	{
		while (!TaskCompletionSource.Task.IsCompleted)
		{
			CancellationToken.ThrowIfCancellationRequested();
			await ReceiveAsync();
		}
	}

	/// <summary>
	/// Receives the next full message from the websocket connection.
	/// </summary>
	/// <returns>
	/// The task object representing the asynchronous operation. The <see cref="ValueTask.Result" /> property on the
	/// task object returns the message bytes.
	/// </returns>
	private async ValueTask ReceiveAsync()
	{
		var ms = new MemoryStream();
		var buffer = new Memory<byte>(new byte[2048]);
		ValueWebSocketReceiveResult result;
		WebSocketMessageType? messageType = null;
		do
		{
			result = await WebSocket.ReceiveAsync(buffer, cancellationToken: CancellationToken);
			ms.Write(buffer[..result.Count].Span);
			messageType ??= result.MessageType;
			if (messageType != result.MessageType)
			{
				Received?.Invoke(this, Error<ObservableWebSocketData>("Inconsistent message types received."));
			}
		} while (!result.EndOfMessage);
		if (ms.Length == 0)
		{
			Received?.Invoke(this, Error<ObservableWebSocketData>("Empty message received."));
		}
		if (messageType is null)
		{
			Received?.Invoke(this, Error<ObservableWebSocketData>("Unknown message type received."));
		}
		ms.Seek(0, SeekOrigin.Begin);
		Received?.Invoke(this, Some(new ObservableWebSocketData(ms.ToArray(), (WebSocketMessageType)messageType!, true)));
		ms.Close();
		ms.Dispose();
	}

	/// <summary>
	/// Split message bytes into smaller blocks and send them over the websocket connection in sequence.
	/// </summary>
	/// <param name="message">The message bytes to send over the websocket connection.</param>
	/// <param name="messageType">The type of websocket message.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	private async ValueTask SendMessageAsChunksAsync(ReadOnlyMemory<byte> message, WebSocketMessageType messageType)
	{
		var chunks = message.Chunk(2048);
		var chunkIndex = 0;
		var chunkUbound = chunks.Count() - 1;
		foreach (var chunk in chunks)
		{
			await WebSocket.SendAsync(chunk, messageType, chunkIndex == chunkUbound, cancellationToken: CancellationToken);
			chunkIndex++;
		}
	}
}
