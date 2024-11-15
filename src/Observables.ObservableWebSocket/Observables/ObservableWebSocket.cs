namespace ReillyDigital.Observables;

using System.Net.WebSockets;
using System.Text.Json;
using static System.Text.Encoding;
using static System.Text.Json.JsonSerializer;

/// <summary>
/// Observable wrapper for a WebSocket connection. Subscribable events are provided for when data is received or sent,
/// as well as events for when the connection is aborted, closed or disposed.
/// </summary>
public class ObservableWebSocket : IDisposable
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

	/// <inheritdoc cref="WebSocket.CloseStatus" />
	public WebSocketCloseStatus? CloseStatus => WebSocket.CloseStatus;

	/// <inheritdoc cref="WebSocket.CloseStatusDescription" />
	public string? CloseStatusDescription => WebSocket.CloseStatusDescription;

	/// <inheritdoc cref="WebSocket.State" />
	public WebSocketState State => WebSocket.State;

	/// <inheritdoc cref="WebSocket.SubProtocol" />
	public string? SubProtocol => WebSocket.SubProtocol;

	/// <summary>
	/// Flag to indicate that the method <see cref="ListenAsync" /> is executing.
	/// </summary>
	private bool IsListenerRunning { get; set; }

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

	/// <summary>
	/// <inheritdoc cref="WebSocket.Abort()" /> Then invokes the <see cref="Aborted" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.Abort()" />
	public void Abort()
	{
		WebSocket.Abort();
		Aborted?.Invoke(this, new());
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.CloseAsync(WebSocketCloseStatus, string?, CancellationToken)" /> Then invokes the
	/// <see cref="Closed" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.CloseAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	public async Task CloseAsync(
		WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken
	)
	{
		await WebSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
		Closed?.Invoke(this, new());
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.CloseAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	/// Uses a status of <see cref="WebSocketCloseStatus.NormalClosure"/>. Then invokes the <see cref="Closed" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.CloseAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	public async Task CloseNormalAsync(string? statusDescription = null, CancellationToken cancellationToken = default)
	{
		await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription, cancellationToken);
		Closed?.Invoke(this, new());
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.CloseOutputAsync(WebSocketCloseStatus, string?, CancellationToken)" /> Then invokes
	/// the <see cref="OutputClosed" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.CloseOutputAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	public async Task CloseOutputAsync(
		WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken
	)
	{
		await WebSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
		OutputClosed?.Invoke(this, new());
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.CloseOutputAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	/// Uses a status of <see cref="WebSocketCloseStatus.NormalClosure"/>. Then invokes the <see cref="Closed" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.CloseOutputAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	public async Task CloseOutputNormalAsync(
		string? statusDescription = null, CancellationToken cancellationToken = default
	)
	{
		await WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, statusDescription, cancellationToken);
		OutputClosed?.Invoke(this, new());
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.Disposed()" /> Then invokes the <see cref="Disposed" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.Disposed()" />
	public void Dispose()
	{
		GC.SuppressFinalize(this);
		WebSocket.Dispose();
		Disposed?.Invoke(this, new());
	}

	/// <summary>
	/// Initialized a continuous listen loop for WebSocket messages. Loop will exit if the WebSocket connection is not
	/// open.
	/// </summary>
	/// <param name="idleTimeout">
	/// A timeout period in milliseconds where if no responses are received then the listen session will be ended.
	/// </param>
	/// <param name="shouldOutputInBlocks">
	/// Flag indicating that partial message should be output as small blocks; otherwise, output will wait until the end
	/// of the message is received before outputting the message as a whole.
	/// </param>
	/// <param name="cancellationToken">Propagates the notification that operations should be canceled.</param>
	public async Task ListenAsync(
		double? idleTimeout = null, bool shouldOutputInBlocks = false, CancellationToken cancellationToken = default
	)
	{
		lock (this)
		{
			if (IsListenerRunning)
			{
				throw new WebSocketException("A listener is already running.");
			}
			IsListenerRunning = true;
		}
		try
		{
			var waitInterval = 100;
			double idleElapsedTime = 0;
			var hasIdleTimeout = idleTimeout is not null;
			if (hasIdleTimeout)
			{
				using var idleCheckTimer = new Timer(
					async (_) =>
					{
						if (idleElapsedTime > idleTimeout)
						{
							await CloseNormalAsync(cancellationToken: cancellationToken);
							throw new OperationCanceledException("The idle timeout has expired.");
						}
						idleElapsedTime += waitInterval;
					}, null, waitInterval, waitInterval
				);
			}
			while (State == WebSocketState.Open)
			{
				await ReceiveFullMessageIgnoringLockAsync(
					shouldOutputInBlocks: shouldOutputInBlocks, cancellationToken: cancellationToken
				);
				if (hasIdleTimeout)
				{
					idleElapsedTime = 0;
				}
			}
		}
		catch (OperationCanceledException) { }
		finally
		{
			IsListenerRunning = false;
		}
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.ReceiveAsync(Memory{byte}, CancellationToken)" /> Then invokes the
	/// <see cref="Received" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.ReceiveAsync(Memory{byte}, CancellationToken)" />
	public async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
		Memory<byte> buffer, CancellationToken cancellationToken
	)
	{
		lock (this)
		{
			if (IsListenerRunning)
			{
				throw new WebSocketException("Cannot receive while a listener is running.");
			}
		}
		var result = await WebSocket.ReceiveAsync(buffer, cancellationToken);
		Received?.Invoke(this, new(buffer[..result.Count], result.MessageType, result.EndOfMessage));
		return result;
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.ReceiveAsync(ArraySegment{byte}, CancellationToken)" /> Then invokes the
	/// <see cref="Received" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.ReceiveAsync(ArraySegment{byte}, CancellationToken)" />
	public async Task<WebSocketReceiveResult> ReceiveAsync(
		ArraySegment<byte> buffer, CancellationToken cancellationToken
	)
	{
		lock (this)
		{
			if (IsListenerRunning)
			{
				throw new WebSocketException("Cannot receive while a listener is running.");
			}
		}
		var result = await WebSocket.ReceiveAsync(buffer, cancellationToken);
		Received?.Invoke(this, new(buffer[..result.Count], result.MessageType, result.EndOfMessage));
		return result;
	}

	/// <inheritdoc cref="ReceiveFullMessageIgnoringLockAsync" />
	public async Task<ObservableWebSocketData> ReceiveFullMessageAsync(CancellationToken cancellationToken = default)
	{
		lock (this)
		{
			if (IsListenerRunning)
			{
				throw new WebSocketException("Cannot receive while a listener is running.");
			}
		}
		return await ReceiveFullMessageIgnoringLockAsync(cancellationToken: cancellationToken);
	}

	/// <inheritdoc cref="ReceiveFullMessageIgnoringLockAsync" />
	public async Task<string> ReceiveFullMessageTextAsync(CancellationToken cancellationToken = default)
	{
		lock (this)
		{
			if (IsListenerRunning)
			{
				throw new WebSocketException("Cannot receive while a listener is running.");
			}
		}
		return await ReceiveFullMessageTextIgnoringLockAsync(cancellationToken: cancellationToken);
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.SendAsync(ReadOnlyMemory{byte}, WebSocketMessageType, bool, CancellationToken)" />
	/// Then invokes the <see cref="Sent" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.SendAsync(ReadOnlyMemory{byte}, WebSocketMessageType, bool, CancellationToken)" />
	public async ValueTask SendAsync(
		ReadOnlyMemory<byte> buffer,
		WebSocketMessageType messageType,
		bool endOfMessage,
		CancellationToken cancellationToken
	)
	{
		await WebSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
		Sent?.Invoke(this, new(buffer, messageType, endOfMessage));
	}

	/// <summary>
	/// <inheritdoc
	/// 	cref="WebSocket.SendAsync(ReadOnlyMemory{byte}, WebSocketMessageType, WebSocketMessageFlags, CancellationToken)"
	/// />
	/// Then invokes the <see cref="Sent" /> event.
	/// </summary>
	/// <inheritdoc
	/// 	cref="WebSocket.SendAsync(ReadOnlyMemory{byte}, WebSocketMessageType, WebSocketMessageFlags, CancellationToken)"
	/// />
	public async ValueTask SendAsync(
		ReadOnlyMemory<byte> buffer,
		WebSocketMessageType messageType,
		WebSocketMessageFlags messageFlags,
		CancellationToken cancellationToken
	)
	{
		await WebSocket.SendAsync(buffer, messageType, messageFlags, cancellationToken);
		Sent?.Invoke(this, new(buffer, messageType, messageFlags.HasFlag(WebSocketMessageFlags.EndOfMessage)));
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.SendAsync(ArraySegment{byte}, WebSocketMessageType, bool, CancellationToken)" />
	/// Then invokes the <see cref="Sent" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.SendAsync(ArraySegment{byte}, WebSocketMessageType, bool, CancellationToken)" />
	public async Task SendAsync(
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
	/// <param name="message">The message to send over the WebSocket connection.</param>
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
	/// Sends a full binary message over the WebSocket connection.
	/// </summary>
	/// <param name="message">The message to send over the WebSocket connection.</param>
	/// <param name="cancellationToken">
	/// The token that propagates the notification that operations should be canceled.
	/// </param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public async ValueTask SendFullMessageBinaryAsync(
		ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default
	) => await SendFullMessageAsync(
		message, messageType: WebSocketMessageType.Binary, cancellationToken: cancellationToken
	);

	/// <summary>
	/// Sends a full text message over the WebSocket connection.
	/// </summary>
	/// <param name="message">The message to send over the WebSocket connection.</param>
	/// <param name="cancellationToken">
	/// The token that propagates the notification that operations should be canceled.
	/// </param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public async ValueTask SendFullMessageTextAsync(
		ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default
	) => await SendFullMessageAsync(
		message, messageType: WebSocketMessageType.Text, cancellationToken: cancellationToken
	);

	/// <summary>
	/// Sends a full text message over the WebSocket connection.
	/// </summary>
	/// <param name="message">The message to send over the WebSocket connection.</param>
	/// <param name="cancellationToken">
	/// The token that propagates the notification that operations should be canceled.
	/// </param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	public async ValueTask SendFullMessageTextAsync(string message, CancellationToken cancellationToken = default) =>
		await SendFullMessageAsync(
			UTF8.GetBytes(message), messageType: WebSocketMessageType.Text, cancellationToken: cancellationToken
		);

	/// <summary>
	/// Receives the next full message from the WebSocket connection asynchronously.
	/// </summary>
	/// <param name="shouldOutputInBlocks">
	/// Flag indicating that partial message should be output as small blocks; otherwise, output will wait until the end
	/// of the message is received before outputting the message as a whole.
	/// </param>
	/// <param name="cancellationToken">Propagates the notification that operations should be canceled.</param>
	/// <returns>The task object representing the asynchronous operation.</returns>
	private async Task<ObservableWebSocketData> ReceiveFullMessageIgnoringLockAsync(
		bool shouldOutputInBlocks = false, CancellationToken cancellationToken = default
	)
	{
		using var ms = new MemoryStream();
		var buffer = new Memory<byte>(new byte[2048]);
		ValueWebSocketReceiveResult result;
		WebSocketMessageType? messageType = null;
		do
		{
			result = await WebSocket.ReceiveAsync(buffer, cancellationToken);
			var blockMessageType = result.MessageType;
			if (blockMessageType == WebSocketMessageType.Close)
			{
				return new ObservableWebSocketData(Array.Empty<byte>(), blockMessageType, false);
			}
			messageType ??= blockMessageType;
			if (messageType != blockMessageType)
			{
				throw new WebSocketException("Inconsistent message types received.");
			}
			var blockSize = result.Count;
			if (blockSize == 0)
			{
				throw new WebSocketException("Empty block received.");
			}
			var blockBytes = buffer[..blockSize];
			if (!shouldOutputInBlocks)
			{
				ms.Write(blockBytes.Span);
				continue;
			}
			Received?.Invoke(this, new(blockBytes, (WebSocketMessageType)messageType!, result.EndOfMessage));
		} while (!result.EndOfMessage);
		ms.Seek(0, SeekOrigin.Begin);
		var returnValue = new ObservableWebSocketData(ms.ToArray(), (WebSocketMessageType)messageType!, true);
		if (!shouldOutputInBlocks)
		{
			Received?.Invoke(this, returnValue);
		}
		return returnValue;
	}

	/// <summary>
	/// Receives the next full message from the WebSocket connection asynchronously.
	/// </summary>
	/// <param name="cancellationToken">Propagates the notification that operations should be canceled.</param>
	/// <returns>
	/// The task object representing the asynchronous operation. The value of <see cref="Task{}.Result" /> is a
	/// <see cref="ObservableWebSocketData" /> containing the result data.
	/// </returns>
	private async Task<string> ReceiveFullMessageTextIgnoringLockAsync(CancellationToken cancellationToken = default)
	{
		var result = await ReceiveFullMessageIgnoringLockAsync(cancellationToken: cancellationToken);
		if (result.MessageType == WebSocketMessageType.Close)
		{
			throw new WebSocketException("WebSocket is closed.");
		}
		return UTF8.GetString(result.Bytes.Span);
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

/// <summary>
/// Observable wrapper for a WebSocket connection. Subscribable events are provided for when a message is received or
/// sent, as well as events for when the connection is aborted, closed or disposed. All messages in an out are of the
/// provided type <see cref="TMessage" />.
/// </summary>
public class ObservableWebSocket<TMessage> : IDisposable
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
	/// An event triggered when a message is received over the WebSocket connection.
	/// </summary>
	public event EventHandler<TMessage>? Received;

	/// <summary>
	/// An event triggered when a message is sent over the WebSocket connection.
	/// </summary>
	public event EventHandler<TMessage>? Sent;

	/// <inheritdoc cref="ObservableWebSocket.CloseStatus" />
	public WebSocketCloseStatus? CloseStatus => WebSocket.CloseStatus;

	/// <inheritdoc cref="ObservableWebSocket.CloseStatusDescription" />
	public string? CloseStatusDescription => WebSocket.CloseStatusDescription;

	/// <inheritdoc cref="ObservableWebSocket.State" />
	public WebSocketState State => WebSocket.State;

	/// <inheritdoc cref="ObservableWebSocket.SubProtocol" />
	public string? SubProtocol => WebSocket.SubProtocol;

	/// <summary>
	/// Flag to indicate that the method <see cref="ListenAsync" /> is executing.
	/// </summary>
	private bool IsListenerRunning { get; set; }

	/// <summary>
	/// The wrapped web socket connection.
	/// </summary>
	private ObservableWebSocket WebSocket { get; }

	/// <summary>
	/// Constructor for this class given an existing WebSocket connection to wrap.
	/// </summary>
	/// <param name="webSocket">The WebSocket connection to be wrapped.</param>
	public ObservableWebSocket(WebSocket webSocket) => WebSocket = new(webSocket);

	/// <inheritdoc cref="ObservableWebSocket(Uri, CancellationToken)" />
	public ObservableWebSocket(string uri, CancellationToken cancellationToken = default) =>
		WebSocket = new(uri, cancellationToken: cancellationToken);

	/// <summary>
	/// Constructor for this class given a URI to use for a new WebSocket connection to wrap.
	/// </summary>
	/// <param name="uri">The URI for a new WebSocket connection to wrap.</param>
	/// <param name="cancellationToken">
	/// A cancellation token for aborting the initialization of the WebSocket connection.
	/// </param>
	public ObservableWebSocket(Uri uri, CancellationToken cancellationToken = default) =>
		WebSocket = new(uri, cancellationToken: cancellationToken);

	/// <summary>
	/// <inheritdoc cref="WebSocket.Abort()" /> Then invokes the <see cref="Aborted" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.Abort()" />
	public void Abort()
	{
		WebSocket.Abort();
		Aborted?.Invoke(this, new());
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.CloseAsync(WebSocketCloseStatus, string?, CancellationToken)" /> Then invokes the
	/// <see cref="Closed" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.CloseAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	public async Task CloseAsync(
		WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken
	)
	{
		await WebSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
		Closed?.Invoke(this, new());
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.CloseAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	/// Uses a status of <see cref="WebSocketCloseStatus.NormalClosure"/>. Then invokes the <see cref="Closed" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.CloseAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	public async Task CloseNormalAsync(string? statusDescription = null, CancellationToken cancellationToken = default)
	{
		await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription, cancellationToken);
		Closed?.Invoke(this, new());
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.CloseOutputAsync(WebSocketCloseStatus, string?, CancellationToken)" /> Then invokes
	/// the <see cref="OutputClosed" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.CloseOutputAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	public async Task CloseOutputAsync(
		WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken
	)
	{
		await WebSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
		OutputClosed?.Invoke(this, new());
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.CloseOutputAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	/// Uses a status of <see cref="WebSocketCloseStatus.NormalClosure"/>. Then invokes the <see cref="Closed" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.CloseOutputAsync(WebSocketCloseStatus, string?, CancellationToken)" />
	public async Task CloseOutputNormalAsync(
		string? statusDescription = null, CancellationToken cancellationToken = default
	)
	{
		await WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, statusDescription, cancellationToken);
		OutputClosed?.Invoke(this, new());
	}

	/// <summary>
	/// <inheritdoc cref="WebSocket.Disposed()" /> Then invokes the <see cref="Disposed" /> event.
	/// </summary>
	/// <inheritdoc cref="WebSocket.Disposed()" />
	public void Dispose()
	{
		GC.SuppressFinalize(this);
		WebSocket.Dispose();
		Disposed?.Invoke(this, new());
	}

	/// <summary>
	/// Initialized a continuous listen loop for WebSocket messages. Loop will exit if the WebSocket connection is not
	/// open.
	/// </summary>
	/// <param name="idleTimeout">
	/// A timeout period in milliseconds where if no responses are received then the listen session will be ended.
	/// </param>
	/// <param name="cancellationToken">Propagates the notification that operations should be canceled.</param>
	public async Task ListenAsync(double? idleTimeout = null, CancellationToken cancellationToken = default)
	{
		lock (this)
		{
			if (IsListenerRunning)
			{
				throw new WebSocketException("A listener is already running.");
			}
			IsListenerRunning = true;
		}
		void handler(object? sender, ObservableWebSocketData data)
		{
			if (data.MessageType != WebSocketMessageType.Text)
			{
				throw new WebSocketException("Received message was not of type Text.");
			}
			Received?.Invoke(
				this, Deserialize<TMessage>(data.Bytes.Span) ?? throw new JsonException("Could not deserialize message.")
			);
		}
		WebSocket.Received += handler;
		try
		{
			await WebSocket.ListenAsync(idleTimeout: idleTimeout, cancellationToken: cancellationToken);
		}
		catch (OperationCanceledException) { }
		finally
		{
			WebSocket.Received -= handler;
			IsListenerRunning = false;
		}
	}

	/// <inheritdoc cref="ObservableWebSocket.ReceiveFullMessageAsync(CancellationToken)" />
	/// <exception cref="WebSocketException" />
	/// <exception cref="JsonException" />
	/// <exception cref="NotSupportedException" />
	public async Task<TMessage> ReceiveAsync(CancellationToken cancellationToken = default)
	{
		lock (this)
		{
			if (IsListenerRunning)
			{
				throw new WebSocketException("Cannot receive while a listener is running.");
			}
		}
		var result = await WebSocket.ReceiveFullMessageAsync(cancellationToken: cancellationToken);
		if (result.MessageType != WebSocketMessageType.Text)
		{
			throw new WebSocketException("Received message was not of type Text.");
		}
		var message =
			Deserialize<TMessage>(result.Bytes.Span) ?? throw new JsonException("Could not deserialize message.");
		Received?.Invoke(this, message);
		return message;
	}

	/// <inheritdoc cref="ObservableWebSocket.SendFullMessageTextAsync(ReadOnlyMemory{byte}, CancellationToken)" />
	public async ValueTask SendAsync(TMessage message, CancellationToken cancellationToken = default)
	{
		await WebSocket.SendFullMessageTextAsync(UTF8.GetBytes(Serialize(message)), cancellationToken: cancellationToken);
		Sent?.Invoke(this, message);
	}
}
