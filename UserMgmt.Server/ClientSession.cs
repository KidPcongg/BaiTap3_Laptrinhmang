using System.Net.Sockets;

namespace UserMgmt.Server;

/// <summary>Một client đang kết nối. Mỗi kết nối TCP có đúng một ClientSession.</summary>
public sealed class ClientSession
{
    public ClientSession(TcpClient tcp)
    {
        Tcp = tcp;
        Endpoint = tcp.Client.RemoteEndPoint?.ToString() ?? "không rõ";
    }

    public Guid Id { get; } = Guid.NewGuid();
    public TcpClient Tcp { get; }
    public string Endpoint { get; }
    public DateTime ConnectedAt { get; } = DateTime.Now;

    /// <summary>null khi chưa đăng nhập. Dispatcher gán sau khi DANGNHAP thành công.</summary>
    public int? UserId { get; set; }
    public string? Username { get; set; }

    public bool IsLoggedIn => UserId is not null;

    /// <summary>Tên hiển thị trong log, ví dụ "192.168.1.5:53012 (cong)".</summary>
    public override string ToString() => Username is null ? Endpoint : $"{Endpoint} ({Username})";

    public void Logout()
    {
        UserId = null;
        Username = null;
    }
}
