# Bài tập 3 – Quản lý người dùng client–server qua TCP

## Cấu trúc

```
UserMgmt.sln
├── UserMgmt.Shared/       Thư viện dùng chung: Request, Response, mã lệnh, mã lỗi, Framing (đóng khung)
├── UserMgmt.Server/       Server console: TcpServer (nhiều client), Dispatcher (xử lý lệnh), Logger
├── UserMgmt.TestClient/   Kiểm tra đường truyền: nhiều client × N chuỗi liên tiếp
├── UserMgmt.Client/       (Tiến/Mạnh tạo trên Windows) WinForms
└── docs/
    ├── GiaoThuc.md                 Tài liệu giao thức (nộp kèm)
    └── client-mau/ServerConnection.cs   Lớp kết nối mẫu cho client
```

## Yêu cầu
- .NET SDK 8 (`dotnet --list-sdks`)
- SQL Server (Windows: bản đã dùng ở Bài 2.2; Mac: Docker)

## Chạy server và test đường truyền

```bash
dotnet build
dotnet run --project UserMgmt.Server                 # cổng mặc định 5050; Ctrl+C để dừng
# terminal khác:
dotnet run --project UserMgmt.TestClient -- 127.0.0.1 5050 20 2
# -> "ĐẠT: 2 client × 20 chuỗi, không chuỗi nào dính liền."
```

Log ghi ra console và `UserMgmt.Server/bin/Debug/net8.0/logs/server-yyyyMMdd.log`.

Thử tay bằng netcat (mỗi dòng là một thông điệp):
```bash
nc 127.0.0.1 5050
{"cmd":"PING","data":{"text":"xin chào"}}
{"cmd":"DANGXUAT"}
```

Máy khác nối vào: dùng IP LAN của máy chạy server (`ipconfig` / `ipconfig getifaddr en0` trên Mac) và mở cổng 5050 trên tường lửa.

## Phân công

| Người | Phần | Tiêu chí |
|---|---|---|
| Công | Shared, TcpServer, TestClient, tài liệu giao thức nháp | 2, 8 |
| Dũng | Dispatcher: DANGKY/DANGNHAP/LAYTHONGTIN + lớp CSDL 2.2 + PasswordHasher | 3, 4, 6, 7 |
| Mạnh | UserMgmt.Client: ServerConnection, form Đăng nhập, Đăng ký (IP, cổng, trạng thái, validate) | 1, 3, 4 |
| Tiến | Form chính, Đăng xuất, FormClosing; hoàn thiện GiaoThuc.md; kiểm thử + báo cáo | 2a, 5 |


### Tiến/Mạnh – việc cần làm
```powershell
dotnet new winforms -n UserMgmt.Client -f net8.0
dotnet sln add UserMgmt.Client
dotnet add UserMgmt.Client reference UserMgmt.Shared
```
Chép `docs/client-mau/ServerConnection.cs` vào project, dựng lại 2 form của Bài 2.2, thêm ô IP, ô cổng, nhãn trạng thái. Client **không** được tham chiếu gói SQL nào.

### Tiến/Mạnh – việc cần làm
Form chính nhận `ServerConnection` + `Dictionary<string,string>` thông tin từ form Đăng nhập. Đăng xuất và `FormClosing` gọi `LogoutAndCloseAsync()` (xem mẫu cuối file ServerConnection.cs).


