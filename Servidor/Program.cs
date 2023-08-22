using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;

internal class Program
{
    private static ConcurrentDictionary<string, WebSocket> _clients = new ConcurrentDictionary<string, WebSocket>();
    private static async Task Main(string[] args)
    {        
        var listener = new HttpListener();
        listener.Prefixes.Add("http://*:1666/");
        listener.Start();

        await Console.Out.WriteLineAsync("Listening for WebSocket connections...") ;

        while (true)
        {
            var context = await listener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                var webSocketContext = await context.AcceptWebSocketAsync(null);
                handleWebSockerConnection(webSocketContext.WebSocket);
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    private static async void handleWebSockerConnection(WebSocket webSocket)
    {
        var clientId = Guid.NewGuid().ToString();
        _clients.TryAdd(clientId, webSocket);
        
        try
        {        
            await SendWelcomeMessage(webSocket,clientId);
            var buffer = new byte[1024];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            while (!result.CloseStatus.HasValue)
            {
                var message = $"{Encoding.UTF8.GetString(buffer, 0, result.Count)}";
                await Console.Out.WriteLineAsync($"{DateTime.UtcNow}: {message}");
                await BroadcastMessage(message, clientId);

                buffer = new byte[1024];
                try
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
                catch (WebSocketException ex) when (ex.Message.Contains("The remote party closed the WebSocket connection without completing the close handshake"))
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed due to remote party closing", CancellationToken.None);
                    }
                    break;
                }

            }
            _clients.TryRemove(clientId, out _);
            if (result.CloseStatus.HasValue)
            {
                if (webSocket.State == WebSocketState.Open) // Verificar o estado antes de fechar
                {
                    await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            await Console.Out.WriteLineAsync($"Error: {ex.Message}");
        }
    }

    private static async Task SendWelcomeMessage(WebSocket webSocket, string clientId)
    {
        var welcomeMessage = $"Welcome {clientId}";
        var buffer = Encoding.UTF8.GetBytes(welcomeMessage);
        await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task BroadcastMessage(string message, string clientId)
    {
        foreach (var client in _clients.Where(kvp => kvp.Key != clientId).Select(kvp => kvp.Value))
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await client.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}