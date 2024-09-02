namespace ReillyDigital.Observables;

using System.Net.WebSockets;

/// <summary>
/// Observable wrapper for a WebSocket connection. Subscribable events are provided for when data is received or sent,
/// as well as events for when the connection is aborted, closed or disposed.
/// </summary>
public class ObservableWebSocket : WebSocket
{
	/// <summary>
	/// An event triggered when the WebSocket connection is aborted.
	/// </summary>
	public event EventHandler? Aborted;

	/// <summary>
	/// An event triggered when the WebSocket connection is closed.
	/// </summary>
	public event EventHandler? Closed;

	/// <summary>
	/// An event triggered when the WebSocket connection is disposed.
	/// </summary>
	public event EventHandler? Disposed;

	/// <summary>
	/// An event triggered when an output closure message is sent over the WebSocket connection.
	/// </summary>
	public event EventHandler? OutputClosed;

	/// <summary>
	/// An event triggered when data is received over the WebSocket connection.
	/// </summary>
	public event EventHandler<ObservableWebSocketData>? Received;

	/// <summary>
	/// An event triggered when data is sent over the WebSocket connection.
	/// </summary>
	public event EventHandler<ObservableWebSocketData>? Sent;

	/// <inheritdoc />
	public override WebSocketCloseStatus? CloseStatus => WebSocket.CloseStatus;

	/// <inheritdoc />
	public override string? CloseStatusDescription => WebSocket.CloseStatusDescription;

	/// <inheritdoc />
	public override WebSocketState State => WebSocket.State;

	/// <inheritdoc />
	public override string? SubProtocol => WebSocket.SubProtocol;

	/// <summary>
	/// The wrapped web socket connection.
	/// </summary>
	private WebSocket WebSocket { get; }

	/// <summary>
	/// Constructor for this class given an existing WebSocket connection to wrap.
	/// </summary>
	/// <param name="webSocket">The WebSocket connection to be wrapped.</param>
	public ObservableWebSocket(WebSocket webSocket)
	{
		if (webSocket.State != WebSocketState.Open)
		{
			throw new TypeInitializationException(
				typeof(ObservableWebSocket).FullName, new ArgumentException($"WebSocket must be open.", nameof(webSocket))
			);
		}
		WebSocket = webSocket;
	}

	/// <inheritdoc cref="ObservableWebSocket(Uri, CancellationToken)" />
	public ObservableWebSocket(string uri, CancellationToken cancellationToken = default)
		: this(new Uri(uri), cancellationToken: cancellationToken) { }

	/// <summary>
	/// Constructor for this class given a URI to use for a new WebSocket connection to wrap.
	/// </summary>
	/// <param name="uri">The URI for a new WebSocket connection to wrap.</param>
	/// <param name="cancellationToken">
	/// A cancellation token for aborting the initialization of the WebSocket connection.
	/// </param>
	public ObservableWebSocket(Uri uri, CancellationToken cancellationToken = default)
	{
		var webSocket = new ClientWebSocket();
		webSocket.ConnectAsync(uri, cancellationToken).Wait(cancellationToken);
		WebSocket = webSocket;
	}

	/// <summary><inheritdoc /> Then invokes the <see cref="Aborted" /> event.</summary>
	/// <inheritdoc />
	public override void Abort()
	{
		WebSocket.Abort();
		Aborted?.Invoke(this, new());
	}

	/// <summary><inheritdoc /> Then invokes the <see cref="Closed" /> event.</summary>
	/// <inheritdoc />
	public override async Task CloseAsync(
		WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken
	)
	{
		await WebSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
		Closed?.Invoke(this, new());
	}

	/// <summary><inheritdoc /> Then invokes the <see cref="OutputClosed" /> event.</summary>
	/// <inheritdoc />
	public override async Task CloseOutputAsync(
		WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken
	)
	{
		await WebSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
		OutputClosed?.Invoke(this, new());
	}

	/// <summary><inheritdoc /> Then invokes the <see cref="Disposed" /> event.</summary>
	/// <inheritdoc />
	public override void Dispose()
	{
		GC.SuppressFinalize(this);
		WebSocket.Dispose();
		Disposed?.Invoke(this, new());
	}

	/// <summary>
	/// Initialized a continuous listen loop for WebSocket messages. Loop will exit if the WebSocket connection is not
	/// open.
	/// </summary>
	/// <param name="shouldOutputInBlocks">
	/// Flag indicating that partial message should be output as small blocks; otherwise, output will wait until the end
	/// of the message is received before outputting the message as a whole.
	/// </param>
	/// <param name="cancellationToken">Propagates the notification that operations should be canceled.</param>
	public async Task ListenAsync(bool shouldOutputInBlocks = false, CancellationToken cancellationToken = default)
	{
		while (State == WebSocketState.Open)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await ReceiveAsync(shouldOutputInBlocks: shouldOutputInBlocks, cancellationToken: cancellationToken);
			await Task.Delay(100, cancellationToken);
		}
	}

	/// <summary><inheritdoc /> Then invokes the <see cref="Received" /> event.</summary>
	/// <inheritdoc />
	public override async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
		Memory<byte> buffer, CancellationToken cancellationToken
	)
	{
		var result = await WebSocket.ReceiveAsync(buffer, cancellationToken);
		Received?.Invoke(this, new(buffer[..result.Count], result.MessageType, result.EndOfMessage));
		return result;
	}

	/// <summary><inheritdoc /> Then invokes the <see cref="Received" /> event.</summary>
	/// <inheritdoc />
	public override async Task<WebSocketReceiveResult> ReceiveAsync(
		ArraySegment<byte> buffer, CancellationToken cancellationToken
	)
	{
		var result = await WebSocket.ReceiveAsync(buffer, cancellationToken);
		Received?.Invoke(this, new(buffer[..result.Count], result.MessageType, result.EndOfMessage));
		return result;
	}

	/// <summary><inheritdoc /> Then invokes the <see cref="Sent" /> event.</summary>
	/// <inheritdoc />
	public override async ValueTask SendAsync(
		ReadOnlyMemory<byte> buffer,
		WebSocketMessageType messageType,
		bool endOfMessage,
		CancellationToken cancellationToken
	)
	{
		await WebSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
		Sent?.Invoke(this, new(buffer, messageType, endOfMessage));
	}

	/// <summary><inheritdoc /> Then invokes the <see cref="Sent" /> event.</summary>
	/// <inheritdoc />
	public override async ValueTask SendAsync(
		ReadOnlyMemory<byte> buffer,
		WebSocketMessageType messageType,
		WebSocketMessageFlags messageFlags,
		CancellationToken cancellationToken
	)
	{
		await WebSocket.SendAsync(buffer, messageType, messageFlags, cancellationToken);
		Sent?.Invoke(this, new(buffer, messageType, messageFlags.HasFlag(WebSocketMessageFlags.EndOfMessage)));
	}

	/// <summary><inheritdoc /> Then invokes the <see cref="Sent" /> event.</summary>
	/// <inheritdoc />
	public override async Task SendAsync(
		ArraySegment<byte> buffer,
		WebSocketMessageType messageType,
		bool endOfMessage,
		CancellationToken cancellationToken
	)
	{
		await WebSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
		Sent?.Invoke(this, new(buffer, messageType, endOfMessage));
	}

	/// <summary>
	/// Sends a full message over the WebSocket connection.
	/// </summary>
	/// <param name="message">The message bytes to send over the WebSocket connection.</param>
	/// <param name="messageType">The type of WebSocket message.</param>
	/// <param name="cancellationToken">
	/// The token that propagates the notification that operations should be canceled.
	/// </param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public async ValueTask SendFullMessageAsync(
		ReadOnlyMemory<byte> message,
		WebSocketMessageType messageType = WebSocketMessageType.Text,
		CancellationToken cancellationToken = default
	)
	{
		await SendMessageAsChunksAsync(message, messageType: messageType, cancellationToken: cancellationToken);
		Sent?.Invoke(this, new(message, messageType, true));
	}

	/// <summary>
	/// Receives the next full message from the WebSocket connection asynchronously.
	/// </summary>
	/// <param name="shouldOutputInBlocks">
	/// Flag indicating that partial message should be output as small blocks; otherwise, output will wait until the end
	/// of the message is received before outputting the message as a whole.
	/// </param>
	/// <param name="cancellationToken">Propagates the notification that operations should be canceled.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	private async ValueTask ReceiveAsync(
		bool shouldOutputInBlocks = false, CancellationToken cancellationToken = default
	)
	{
		var ms = new MemoryStream();
		var buffer = new Memory<byte>(new byte[2048]);
		ValueWebSocketReceiveResult result;
		WebSocketMessageType? messageType = null;
		do
		{
			result = await WebSocket.ReceiveAsync(buffer, cancellationToken);
			var blockSize = result.Count;
			if (blockSize == 0)
			{
				throw new("Empty block received.");
			}
			var blockMessageType = result.MessageType;
			messageType ??= blockMessageType;
			if (messageType != blockMessageType)
			{
				throw new("Inconsistent message types received.");
			}
			var blockBytes = buffer[..blockSize];
			if (!shouldOutputInBlocks)
			{
				ms.Write(blockBytes.Span);
				continue;
			}
			Received?.Invoke(this, new(blockBytes, (WebSocketMessageType)messageType!, result.EndOfMessage));
		} while (!result.EndOfMessage);
		if (!shouldOutputInBlocks)
		{
			ms.Seek(0, SeekOrigin.Begin);
			Received?.Invoke(this, new(ms.ToArray(), (WebSocketMessageType)messageType!, true));
		}
		ms.Close();
		ms.Dispose();
	}

	/// <summary>
	/// Split message bytes into smaller blocks and send them over the WebSocket connection in sequence.
	/// </summary>
	/// <param name="message">The message bytes to send over the WebSocket connection.</param>
	/// <param name="messageType">Indicates whether the application is sending a binary or text message.</param>
	/// <param name="cancellationToken">
	/// The token that propagates the notification that operations should be canceled.
	/// </param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	private async ValueTask SendMessageAsChunksAsync(
		ReadOnlyMemory<byte> message, WebSocketMessageType messageType, CancellationToken cancellationToken = default
	)
	{
		var chunks = message.Chunk(2048);
		var chunkIndex = 0;
		var chunkUbound = chunks.Count() - 1;
		foreach (var chunk in chunks)
		{
			await WebSocket.SendAsync(chunk, messageType, chunkIndex == chunkUbound, cancellationToken);
			chunkIndex++;
		}
	}
}
