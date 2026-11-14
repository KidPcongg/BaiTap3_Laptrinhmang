using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using UserMgmt.Shared;

namespace UserMgmt.Server;

/// <summary>
/// Server TCP phục vụ nhiều client cùng lúc.
///  - Nghe ở IPAddress.Any để máy khác trong mạng nối vào được.
///  - Mỗi client một tác vụ riêng (gọi HandleClientAsync KHÔNG await).
///  - Danh sách client là ConcurrentDictionary vì nhiều tác vụ cùng thêm/gỡ.
///  - Gỡ client trong khối finally, xử lý đủ 3 kiểu rời đi.
///  - Dừng bằng CancellationToken, bỏ qua OperationCanceledException, gọi Stop trong finally.
/// </summary>
public sealed class TcpServer
{
    private readonly int _port;
    private readonly Dispatcher _dispatcher;
    private readonly ConcurrentDictionary<Guid, ClientSession> _clients = new();
    private readonly ConcurrentDictionary<Guid, Task> _clientTasks = new();

    public TcpServer(int port, Dispatcher dispatcher)
    {
        _port = port;
        _dispatcher = dispatcher;
    }

    public int ClientCount => _clients.Count;

    public async Task RunAsync(CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        try
        {
            listener.Start();
            Logger.Info($"Server đang nghe ở 0.0.0.0:{_port}. Bấm Ctrl+C để dừng.");

            while (!ct.IsCancellationRequested)
            {
                TcpClient tcp = await listener.AcceptTcpClientAsync(ct);
                var session = new ClientSession(tcp);

                // KHÔNG await: giao client cho một tác vụ riêng rồi quay lại nghe ngay.
                Task task = HandleClientAsync(session, ct);
                _clientTasks[session.Id] = task;
            }
        }
        catch (OperationCanceledException)
        {
            // Dừng chủ động bằng Ctrl+C: bỏ qua.
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            Logger.Error($"Cổng {_port} đang bị chương trình khác dùng. Hãy tắt tiến trình server cũ rồi chạy lại.");
        }
        finally
        {
            listener.Stop();
            Logger.Info("Đã ngừng nhận kết nối mới. Đang đóng các client còn lại...");

            foreach (var session in _clients.Values)
                session.Tcp.Close();

            var remaining = _clientTasks.Values.ToArray();
            await Task.WhenAny(Task.WhenAll(remaining), Task.Delay(TimeSpan.FromSeconds(5)));
            Logger.Info("Server đã dừng.");
        }
    }

    private async Task HandleClientAsync(ClientSession session, CancellationToken ct)
    {
        _clients[session.Id] = session;
        Logger.Info($"{session} đã kết nối. Đang có {_clients.Count} client.");
        string reason = "không rõ";

        try
        {
            using TcpClient tcp = session.Tcp;
            EnableKeepAlive(tcp.Client, session);

            NetworkStream stream = tcp.GetStream();
            using StreamReader reader = Framing.CreateReader(stream);
            using StreamWriter writer = Framing.CreateWriter(stream);

            while (true)
            {
                string? line = await Framing.ReadLineAsync(reader, ct);
                if (line is null)
                {
                    // Kiểu 2: client đóng socket (bấm nút đỏ, FormClosing gọi Close) mà không gửi DANGXUAT.
                    reason = "client đóng kết nối";
                    break;
                }

                Request? request;
                try
                {
                    request = Framing.Deserialize<Request>(line);
                }
                catch (JsonException)
                {
                    request = null;
                }

                if (request is null || string.IsNullOrWhiteSpace(request.Cmd))
                {
                    Logger.Warn($"{session} gửi thông điệp không hợp lệ: {Truncate(line)}");
                    await Framing.WriteAsync(writer, Response.Fail(ErrorCodes.BadRequest, "Yêu cầu không đúng định dạng."), ct);
                    continue;
                }

                Logger.Info($"{session} -> {request.Cmd}");
                Response response = await _dispatcher.HandleAsync(request, session);
                await Framing.WriteAsync(writer, response, ct);
                Logger.Info($"{session} <- {response.Code}");

                if (request.Cmd == Commands.DangXuat)
                {
                    // Kiểu 1: rời đàng hoàng bằng lệnh DANGXUAT.
                    reason = "đăng xuất";
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            reason = "server dừng";
        }
        catch (IOException ex)
        {
            // Kiểu 3: mất mạng đột ngột / client bị kill. Keepalive hết hạn hoặc nhận RST sẽ rơi vào đây.
            reason = $"mất kết nối đột ngột ({(ex.InnerException as SocketException)?.SocketErrorCode.ToString() ?? ex.GetType().Name})";
        }
        catch (SocketException ex)
        {
            reason = $"lỗi socket ({ex.SocketErrorCode})";
        }
        catch (InvalidDataException ex)
        {
            reason = $"thông điệp không hợp lệ ({ex.Message})";
        }
        catch (ObjectDisposedException)
        {
            reason = "server dừng";
        }
        catch (Exception ex)
        {
            // Tác vụ nền không được chết lặng lẽ (mục V của đề).
            reason = "lỗi không lường trước";
            Logger.Error($"{session} lỗi không lường trước: {ex}");
        }
        finally
        {
            _clients.TryRemove(session.Id, out _);
            _clientTasks.TryRemove(session.Id, out _);
            Logger.Info($"{session} rời đi: {reason}. Còn {_clients.Count} client.");
        }
    }

    /// <summary>
    /// TCP không tự báo khi bên kia mất mạng. Bật keepalive: im lặng 30s thì hỏi thăm,
    /// 3 lần × 5s không trả lời thì hệ điều hành báo lỗi và ReadLineAsync ném IOException.
    /// </summary>
    private static void EnableKeepAlive(Socket socket, ClientSession session)
    {
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
        }
        catch (Exception ex) when (ex is SocketException or PlatformNotSupportedException)
        {
            Logger.Warn($"{session}: hệ điều hành không hỗ trợ đủ tuỳ chọn keepalive ({ex.Message}).");
        }
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "...";
}
