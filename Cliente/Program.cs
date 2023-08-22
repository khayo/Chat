using Microsoft.Extensions.Configuration;
using System.Net.WebSockets;
using System.Text;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
           .AddUserSecrets<Program>()
           .Build();

        using (var clientWebSocket = new ClientWebSocket())
        {
            var serverUri = new Uri(config["ServerUri"]); // ws://ipServer:portaServer
            await clientWebSocket.ConnectAsync(serverUri, CancellationToken.None);

            await Console.Out.WriteLineAsync("Enter your username: ");
            var username = Console.ReadLine();

            await SendMessage(clientWebSocket, $"User {username} connected.");

            await Console.Out.WriteLineAsync("Connected to server. Start typing and press Enter to send messages.");
            
            Task.Run(async () =>
            {
                while (true)
                {
                    string receivedMessage = await ReceiveMessage(clientWebSocket);
                    await Console.Out.WriteLineAsync(receivedMessage);
                }
            });

            while (true)
            {
                var message = Console.ReadLine();
                await SendMessage(clientWebSocket, $"{username}: {message}");
            }
        }
    }

    private static async Task SendMessage(ClientWebSocket clientWebSocket, string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await clientWebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    static async Task<string> ReceiveMessage(ClientWebSocket clientWebSocket)
    {
        byte[] buffer = new byte[1024];
        WebSocketReceiveResult result = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
}