using Microsoft.Data.SqlClient;
using UserMgmt.Server;
using UserMgmt.Server.Data;
using UserMgmt.Shared;

// Cách chạy: dotnet run --project UserMgmt.Server -- [cổng]
//
// Chuỗi kết nối CSDL lấy từ biến môi trường USERMGMT_DB; không đặt thì dùng mặc định bên dưới
// (SQL Server cài trên máy Windows, đăng nhập bằng tài khoản Windows)
//   Windows (PowerShell): $env:USERMGMT_DB = "Server=localhost\SQLEXPRESS;Database=QuanLyNguoiDung;Integrated Security=True;TrustServerCertificate=True"
//   Mac (Docker):         export USERMGMT_DB="Server=localhost,1433;Database=QuanLyNguoiDung;User Id=sa;Password=<mật khẩu>;TrustServerCertificate=True"
const string DefaultConnectionString =
    "Server=localhost;Database=QuanLyNguoiDung;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5";

int port = Protocol.DefaultPort;
if (args.Length > 0 && (!int.TryParse(args[0], out port) || port < 1024 || port > 49151))
{
    Console.WriteLine("Cổng phải là số trong dải 1024–49151.");
    return 1;
}

string connectionString = Environment.GetEnvironmentVariable("USERMGMT_DB") ?? DefaultConnectionString;
var repository = new NguoiDungRepository(connectionString);

// Ghi ra máy chủ và tên CSDL đang dùng, KHÔNG ghi mật khẩu
var csb = new SqlConnectionStringBuilder(connectionString);
try
{
    await repository.CheckConnectionAsync();
    Logger.Info($"Đã kết nối CSDL {csb.InitialCatalog} trên {csb.DataSource}.");
}
catch (SqlException ex)
{
    // Vẫn cho server chạy để thử đường truyền (PING); các lệnh cần CSDL sẽ trả E_SERVER
    Logger.Error($"Không kết nối được CSDL {csb.InitialCatalog} trên {csb.DataSource}: {ex.Message}");
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;          // không để tiến trình bị kill ngay, cho server tự dọn dẹp
    Logger.Info("Nhận Ctrl+C, đang dừng server...");
    cts.Cancel();
};

var server = new TcpServer(port, new Dispatcher(repository));
await server.RunAsync(cts.Token);
return 0;