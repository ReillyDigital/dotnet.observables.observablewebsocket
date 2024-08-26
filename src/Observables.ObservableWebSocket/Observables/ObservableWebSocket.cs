namespace ReillyDigital.Observables;

using System.Net.WebSockets;

public class ObservableWebSocket : IDisposable
{
	public event EventHandler<EventArgs>? Aborted;

	public event EventHandler<ReadOnlyMemory<byte>>? BinarySent;

	public event EventHandler<EventArgs>? Closed;

	public event EventHandler<EventArgs>? CloseSent;

	public event EventHandler<EventArgs>? Disposed;

	public event EventHandler<ReadOnlyMemory<byte>>? MessageSent;

	public event EventHandler<EventArgs>? OutputClosed;

	public event EventHandler<ReadOnlyMemory<byte>>? Received;

	public WebSocketCloseStatus? CloseStatus => WebSocket.CloseStatus;

	public string? CloseStatusDescription => WebSocket.CloseStatusDescription;

	public WebSocketState State => WebSocket.State;

	public string? SubProtocol => WebSocket.SubProtocol;

	private WebSocket WebSocket { get; }

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

	public ObservableWebSocket(string uri) : this(new Uri(uri)) { }

	public ObservableWebSocket(Uri uri)
	{
		var clientWebSocket = new ClientWebSocket();
		clientWebSocket.ConnectAsync(uri, new()).Wait();
		WebSocket = clientWebSocket;
	}

	public void Abort()
	{
		WebSocket.Abort();
		Aborted?.Invoke(this, new());
	}

	public async Task CloseAsync(
		WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await WebSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
		Closed?.Invoke(this, new());
	}

	public async Task CloseOutputAsync(
		WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await WebSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
		OutputClosed?.Invoke(this, new());
	}

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

	public async Task SendCloseAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
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
