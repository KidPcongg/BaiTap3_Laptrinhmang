using System.Diagnostics;
using System.Net.Sockets;
using UserMgmt.Shared;

// Kiểm tra đường truyền (Bước 4 của đề): nhiều client, mỗi client gửi N chuỗi LIÊN TIẾP
// (gửi hết rồi mới đọc) để ép TCP gộp/cắt gói. Đạt khi mọi client nhận đủ N phản hồi đúng thứ tự,
// không chuỗi nào dính liền hay bị cắt.
//
// Cách chạy: dotnet run --project UserMgmt.TestClient -- [ip] [cổng] [số chuỗi] [số client]
// Mặc định:  127.0.0.1 5050 20 2

string host = args.Length > 0 ? args[0] : "127.0.0.1";
int port = args.Length > 1 ? int.Parse(args[1]) : Protocol.DefaultPort;
int count = args.Length > 2 ? int.Parse(args[2]) : 20;
int clients = args.Length > 3 ? int.Parse(args[3]) : 2;

Console.WriteLine($"Nối {clients} client tới {host}:{port}, mỗi client gửi {count} chuỗi liên tiếp...");
var sw = Stopwatch.StartNew();
var results = await Task.WhenAll(Enumerable.Range(1, clients).Select(i => RunClientAsync($"client{i}")));
sw.Stop();

foreach (var r in results) Console.WriteLine(r.Summary);
bool allOk = results.All(r => r.Ok);
Console.WriteLine(allOk
    ? $"ĐẠT: {clients} client × {count} chuỗi, không chuỗi nào dính liền. ({sw.ElapsedMilliseconds} ms)"
    : "CHƯA ĐẠT: xem chi tiết ở trên.");
return allOk ? 0 : 1;

async Task<(bool Ok, string Summary)> RunClientAsync(string name)
{
    try
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port);
        var stream = tcp.GetStream();
        using var reader = Framing.CreateReader(stream);
        using var writer = Framing.CreateWriter(stream);

        // Nội dung cố tình chứa tiếng Việt và ký tự xuống dòng để chứng minh đóng khung an toàn.
        string Text(int i) => $"{name}-{i:D2} xin chào Việt Nam\ncó xuống dòng";

        for (int i = 1; i <= count; i++)
            await Framing.WriteAsync(writer, new Request(Commands.Ping, new() { [Fields.Text] = Text(i) }));

        for (int i = 1; i <= count; i++)
        {
            var resp = await Framing.ReadAsync<Response>(reader);
            if (resp is null) return (false, $"{name}: server đóng kết nối sau {i - 1}/{count} phản hồi");
            var echo = resp.Data?.GetValueOrDefault(Fields.Text);
            if (!resp.Ok || echo != Text(i))
                return (false, $"{name}: phản hồi thứ {i} sai. Nhận: {Framing.Serialize(resp)}");
        }

        await Framing.WriteAsync(writer, new Request(Commands.DangXuat));
        await Framing.ReadAsync<Response>(reader);
        return (true, $"{name}: nhận đủ {count}/{count} phản hồi đúng thứ tự");
    }
    catch (SocketException ex)
    {
        return (false, $"{name}: không nối được tới server ({ex.SocketErrorCode})");
    }
    catch (IOException ex)
    {
        return (false, $"{name}: mất kết nối ({ex.Message})");
    }
}
