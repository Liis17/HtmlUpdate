using System.Net.WebSockets;
internal class Program
{
    private static async Task Main(string[] args)
    {
        string filePath = string.Empty;
        if (args.Length != 0)
        {
            Console.WriteLine($"Будет использован файл {args[0].Trim()}");
            filePath = args[0].Trim();
        }
        else
        {
            Console.WriteLine("Введи путь к HTML-файлу:");
            filePath = Console.ReadLine().Trim();
        }
        

        if (!File.Exists(filePath))
        {
            Console.WriteLine("Файл не найден.");
            return;
        }

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://localhost:5005");

        var app = builder.Build();

        var clients = new List<WebSocket>();

        var watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath))
        {
            Filter = Path.GetFileName(filePath),
            NotifyFilter = NotifyFilters.LastWrite
        };
        watcher.Changed += async (sender, e) =>
        {
            foreach (var client in clients.ToArray())
            {
                if (client.State == WebSocketState.Open)
                {
                    await client.SendAsync(new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes("reload")), WebSocketMessageType.Text, true, default);
                }
            }
        };
        watcher.EnableRaisingEvents = true;

        app.MapGet("/", async context =>
        {
            string html = await File.ReadAllTextAsync(filePath);
            string script = @"<script>
    let ws;
    function connect() {
        ws = new WebSocket('ws://' + location.host + '/ws');
        ws.onmessage = (event) => {
            if (event.data === 'reload') location.reload();
        };
        ws.onclose = () => {
            setTimeout(connect, 1000); // Переподключение через 1 сек
        };
        ws.onerror = () => {
            ws.close();
        };
    }
    connect();
</script>";
            html = html.Replace("</body>", script + "</body>");
            await context.Response.WriteAsync(html);
        });

        app.UseWebSockets();
        app.Map("/ws", async context =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var ws = await context.WebSockets.AcceptWebSocketAsync();
                clients.Add(ws);

                var buffer = new byte[4096];
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), default);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, default);
                    }
                }
                clients.Remove(ws);
                await Task.Delay(-1);
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        });

        Console.WriteLine("Сервер запущен. http://localhost:5005 в браузере.");
        await app.RunAsync();
    }
}