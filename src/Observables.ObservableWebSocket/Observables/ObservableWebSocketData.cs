namespace ReillyDigital.Observables;

using System.Net.WebSockets;

public record ObservableWebSocketData(
	ReadOnlyMemory<byte> Bytes, WebSocketMessageType MessageType, bool IsEndOfMessage
);
