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
	public event EventHandler<EventArgs>? Aborted;

	/// <summary>
	/// An event triggered when binary data is sent over the websocket connection.
	/// </summary>
	public event EventHandler<ReadOnlyMemory<byte>>? BinarySent;

	/// <summary>
	/// An event triggered when the websocket connection is closed.
	/// </summary>
	public event EventHandler<EventArgs>? Closed;

	/// <summary>
	/// An event triggered when an output closure message is sent over the websocket connection.
	/// </summary>
	public event EventHandler<EventArgs>? CloseSent;

	/// <summary>
	/// An event triggered when the websocket connection is disposed.
	/// </summary>
	public event EventHandler<EventArgs>? Disposed;

	/// <summary>
	/// An event triggered when text data is sent over the websocket connection.
	/// </summary>
	public event EventHandler<ReadOnlyMemory<byte>>? MessageSent;

	/// <summary>
	/// An event triggered when data is received over the websocket connection.
	/// </summary>
	public event EventHandler<ReadOnlyMemory<byte>>? Received;

	/// <summary>
	/// Indicates the reason why the remote endpoint initiated the close handshake.
	/// </summary>
	public WebSocketCloseStatus? CloseStatus => WebSocket.CloseStatus;

	/// <summary>
	/// Allows the remote endpoint to describe the reason why the connection was closed.
	/// </summary>
	public string? CloseStatusDescription => WebSocket.CloseStatusDescription;

	/// <summary>
	/// Returns the current state of the WebSocket connection.
	/// </summary>
	public WebSocketState State => WebSocket.State;

	/// <summary>
	/// Gets the subprotocol that was negotiated during the opening handshake.
	/// </summary>
	public string? SubProtocol => WebSocket.SubProtocol;

	/// <summary>
	/// The wrapped web socket connection.
	/// </summary>
	private WebSocket WebSocket { get; }

	/// <summary>
	/// Constructor for this class given an existing websocket connection to wrap.
	/// </summary>
	/// <param name="webSocket">The websocket connection to be wrapped.</param>
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

	/// <summary>
	/// Constructor for this class given a URI to use for a new websocket connection to wrap.
	/// </summary>
	/// <param name="uri">The URI for a new websocket connection to wrap.</param>
	public ObservableWebSocket(string uri) : this(new Uri(uri)) { }

	public ObservableWebSocket(Uri uri)
	{
		var clientWebSocket = new ClientWebSocket();
		clientWebSocket.ConnectAsync(uri, new()).Wait();
		WebSocket = clientWebSocket;
	}

	/// <summary>
	/// Aborts the wrapped websocket connection and invokes the <see cref="Aborted" /> event.
	/// </summary>
	public void Abort()
	{
		WebSocket.Abort();
		Aborted?.Invoke(this, new());
	}

	/// <summary>
	/// Closes the wrapped websocket connection and invokes the <see cref="Closed" /> event.
	/// </summary>
	/// <param name="status">The status of the closure.</param>
	/// <param name="description">The description of the closure.</param>
	/// <param name="cancellationToken">A cancellation token for the async request.</param>
	/// <exception cref="OperationCanceledException" />
	public async Task CloseAsync(
		WebSocketCloseStatus status, string? description, CancellationToken cancellationToken = default!
	)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await WebSocket.CloseAsync(status, description, cancellationToken);
		Closed?.Invoke(this, new());
	}

	/// <summary>
	/// Disposes the wrapped websocket connection and invokes the <see cref="Disposed" /> event.
	/// </summary>
	public void Dispose()
	{
		WebSocket.Dispose();
		Disposed?.Invoke(this, new());
	}

	public async Task<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var ms = new MemoryStream();
		var buffer = new Memory<byte>(new byte[2048]);
		ValueWebSocketReceiveResult result;
		do
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				result = await WebSocket.ReceiveAsync(buffer, cancellationToken);
			}
			catch
			{
				return null;
			}
			ms.Write(buffer[..result.Count].Span);
		} while (!result.EndOfMessage);
		ms.Seek(0, SeekOrigin.Begin);
		var receivedBytes = ms.ToArray();
		ms.Close();
		ms.Dispose();
		Received?.Invoke(this, receivedBytes);
		return receivedBytes;
	}

	public async Task SendBinaryAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await SendDataChunksAsync(data, WebSocketMessageType.Binary, cancellationToken);
		BinarySent?.Invoke(this, data);
	}

	public async Task SendCloseAsync(CancellationToken cancellationToken) =>
		await SendCloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);

	public async Task SendCloseAsync(
		WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await WebSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
		CloseSent?.Invoke(this, new());
	}

	public async Task SendMessageAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await SendDataChunksAsync(data, WebSocketMessageType.Text, cancellationToken);
		MessageSent?.Invoke(this, data);
	}

	private async Task SendDataChunksAsync(
		ReadOnlyMemory<byte> data, WebSocketMessageType messageType, CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var chunks = data.Chunk(2048);
		var chunkIndex = 0;
		var chunkUbound = chunks.Count() - 1;
		foreach (var chunk in chunks)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await WebSocket.SendAsync(chunk, messageType, chunkIndex == chunkUbound, cancellationToken);
			chunkIndex++;
		}
	}
}
