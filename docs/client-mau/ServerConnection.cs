// MẪU CHO NGƯỜI 3 – chép vào project UserMgmt.Client (namespace tuỳ chỉnh).
// Một kết nối duy nhất dùng chung cho cả form Đăng nhập, Đăng ký và form chính.
using System.Net.Sockets;
using UserMgmt.Shared;

namespace UserMgmt.Client;

public sealed class ServerConnection : IDisposable
{
    private TcpClient? _tcp;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private TaskCompletionSource<Response>? _pending;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private Task? _receiveLoop;
    private bool _expectClose;     // true khi chính mình chủ động đăng xuất / đóng

    /// <summary>
    /// Báo trạng thái kết nối cho giao diện. CHÚ Ý: có thể được gọi từ luồng nền,
    /// form phải dùng BeginInvoke khi cập nhật nhãn.
    /// </summary>
    public event Action<string>? StatusChanged;

    /// <summary>true khi socket còn mở VÀ vòng lặp nhận còn chạy (server chưa đóng).</summary>
    public bool IsConnected => _tcp is not null && _receiveLoop is { IsCompleted: false };

    public async Task ConnectAsync(string host, int port)
    {
        Dispose();
        _expectClose = false;
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(5));
        NetworkStream stream = _tcp.GetStream();
        _reader = Framing.CreateReader(stream);
        _writer = Framing.CreateWriter(stream);
        StatusChanged?.Invoke($"Đã kết nối {host}:{port}");
        _receiveLoop = ReceiveLoopAsync(_tcp, _reader);   // gọi một lần, KHÔNG await, để chạy nền
    }

    /// <summary>Gửi một yêu cầu và chờ phản hồi (tối đa 10 giây).</summary>
    /// <exception cref="IOException">Mất kết nối.</exception>
    /// <exception cref="TimeoutException">Server không trả lời.</exception>
    public async Task<Response> SendAsync(Request request)
    {
        if (_writer is null || !IsConnected) throw new IOException("Chưa kết nối tới server.");

        await _sendLock.WaitAsync();     // mỗi lúc chỉ có một yêu cầu đang chờ
        try
        {
            _pending = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
            await Framing.WriteAsync(_writer, request);
            return await _pending.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            _pending = null;
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(TcpClient tcp, StreamReader reader)
    {
        try
        {
            while (true)
            {
                Response? response = await Framing.ReadAsync<Response>(reader);
                if (response is null) break;            // server đóng kết nối
                _pending?.TrySetResult(response);
            }
        }
        catch (IOException) { }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
        catch (System.Text.Json.JsonException) { }

        // Chủ động đóng (đăng xuất, Dispose, hoặc đã nối lại bằng kết nối mới) => không phải sự cố.
        bool closedByUser = _expectClose || !ReferenceEquals(tcp, _tcp);
        if (!closedByUser) _pending?.TrySetException(new IOException("Mất kết nối tới server."));
        StatusChanged?.Invoke(closedByUser ? "Đã ngắt kết nối" : "Mất kết nối tới server");
    }

    /// <summary>Gọi ở nút Đăng xuất và trong FormClosing.</summary>
    public async Task LogoutAndCloseAsync()
    {
        _expectClose = true;
        try
        {
            if (IsConnected) await SendAsync(new Request(Commands.DangXuat));
        }
        catch (Exception ex) when (ex is IOException or SocketException or TimeoutException) { }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        _expectClose = true;
        _tcp?.Close();
        _tcp = null;
        _reader = null;
        _writer = null;
    }
}

/* ======= CÁCH DÙNG TRONG FORM ĐĂNG NHẬP =======

private async void btnDangNhap_Click(object sender, EventArgs e)
{
    btnDangNhap.Enabled = false;                       // khoá nút khi chờ
    try
    {
        if (!Connection.IsConnected)
            await Connection.ConnectAsync(txtServerIp.Text.Trim(), (int)numPort.Value);

        Response resp = await Connection.SendAsync(new Request(Commands.DangNhap, new()
        {
            [Fields.Username] = txtTenDangNhap.Text.Trim(),
            [Fields.Password] = txtMatKhau.Text,
        }));

        if (resp.Ok) { new FormChinh(Connection, resp.Data!).Show(); Hide(); }
        else { MessageBox.Show(resp.Message, "Đăng nhập"); txtMatKhau.Clear(); }
    }
    catch (SocketException) { MessageBox.Show("Không kết nối được tới server. Kiểm tra IP và cổng."); }
    catch (IOException)     { MessageBox.Show("Mất kết nối tới server."); }
    catch (TimeoutException){ MessageBox.Show("Server không phản hồi."); }
    finally { btnDangNhap.Enabled = true; }
}

// Cập nhật nhãn trạng thái an toàn từ luồng nền:
Connection.StatusChanged += msg => lblTrangThai.BeginInvoke(() => lblTrangThai.Text = msg);

// FormChinh: hoãn việc đóng cửa sổ cho tới khi gửi xong DANGXUAT, nếu không tiến trình có thể
// thoát trước khi kịp gửi.
private bool _daNgatKetNoi;
private async void FormChinh_FormClosing(object sender, FormClosingEventArgs e)
{
    if (_daNgatKetNoi) return;
    e.Cancel = true;
    _daNgatKetNoi = true;
    await Connection.LogoutAndCloseAsync();
    Close();
}
*/
