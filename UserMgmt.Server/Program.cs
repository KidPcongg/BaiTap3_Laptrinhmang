using UserMgmt.Server;
using UserMgmt.Shared;

// Cách chạy: dotnet run --project UserMgmt.Server -- [cổng]
int port = Protocol.DefaultPort;
if (args.Length > 0 && (!int.TryParse(args[0], out port) || port < 1024 || port > 49151))
{
    Console.WriteLine("Cổng phải là số trong dải 1024–49151.");
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;          // không để tiến trình bị giết ngay, cho server tự dọn dẹp
    Logger.Info("Nhận Ctrl+C, đang dừng server...");
    cts.Cancel();
};

var server = new TcpServer(port, new Dispatcher());
await server.RunAsync(cts.Token);
return 0;
