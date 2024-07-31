using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class WebsocketServer
{
    private static readonly ConcurrentDictionary<string, WebSocket> _clients = new ConcurrentDictionary<string, WebSocket>();

    public static void Start()
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/");
        listener.Start();
        Console.WriteLine("WebSocket server started. Listening on ws://localhost:8080/");
        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            if (context.Request.IsWebSocketRequest)
            {
                Task.Run(() => ProcessWebSocketRequest(context));
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    private static async Task ProcessWebSocketRequest(HttpListenerContext context)
    {
        HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
        WebSocket webSocket = webSocketContext.WebSocket;
        string clientId = Guid.NewGuid().ToString();
        Console.WriteLine($"WebSocket connection established with ID: {clientId}");
        _clients[clientId] = webSocket;
        string requestUrl = context.Request.Url.AbsolutePath;
        ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024]);
        WebSocketReceiveResult result = null;

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                string message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                Console.WriteLine($"Received from {clientId}: {message}");
                string responseMessage = GetResponseMessage(requestUrl, message);
                ArraySegment<byte> responseBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(responseMessage));
                await webSocket.SendAsync(responseBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            webSocket.Dispose();
            Console.WriteLine($"WebSocket connection closed for ID: {clientId}");
        }
    }

    private static void BroadcastMessages()
    {
        while (true)
        {
            string messageToBroadcast = "Hello to all clients!";
            BroadcastMessage(messageToBroadcast);
            Thread.Sleep(10000);
        }
    }

    private static void BroadcastMessage(string message)
    {
        var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));

        foreach (var client in _clients.Values)
        {
            if (client.State == WebSocketState.Open)
            {
                try
                {
                    client.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult(); // Blocking call
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Broadcast error: " + ex.Message);
                }
            }
        }
    }

    public static void SendMessageToClient(string clientId, string message)
    {
        if (_clients.TryGetValue(clientId, out WebSocket webSocket))
        {
            if (webSocket.State == WebSocketState.Open)
            {
                var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
                try
                {
                    webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult(); // Blocking call
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message to client {clientId}: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine($"WebSocket for client {clientId} is not open.");
            }
        }
        else
        {
            Console.WriteLine($"Client {clientId} not found.");
        }
    }

    private static string GetResponseMessage(string requestUrl, string data)
    {
        // Example response logic based on URL and data
        if (requestUrl.StartsWith("/echo", StringComparison.OrdinalIgnoreCase))
        {
            return $"Echo: {data}";
        }
        else if (requestUrl.StartsWith("/reverse", StringComparison.OrdinalIgnoreCase))
        {
            char[] dataArray = data.ToCharArray();
            Array.Reverse(dataArray);
            return $"Reversed: {new string(dataArray)}";
        }
        else if (requestUrl.StartsWith("/uppercase", StringComparison.OrdinalIgnoreCase))
        {
            return $"Uppercase: {data.ToUpper()}";
        }
        else
        {
            return $"Unknown request URL: {requestUrl}";
        }
    }
}
