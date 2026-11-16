# TÀI LIỆU GIAO THỨC – Ứng dụng quản lý người dùng (Bài tập 3)

## 1. Thông số kết nối

| Mục | Giá trị |
|---|---|
| Giao thức vận chuyển | TCP (Transmission Control Protocol) |
| Cổng server | **5050** (nằm trong dải 1024–49151; có thể thay đổi bằng tham số dòng lệnh) |
| Địa chỉ lắng nghe | `0.0.0.0` (`IPAddress.Any`) – cho phép các máy khác trong mạng LAN kết nối vào |
| Bảng mã (Encoding) | **UTF-8 không BOM** (`new UTF8Encoding(false)`), thống nhất ở cả Client và Server |
| Mô hình trao đổi | Yêu cầu – Phản hồi (Request – Response): Client gửi 1 Request, Server phản hồi đúng 1 Response tương ứng theo thứ tự |
| Quản lý phiên | Gắn với kết nối TCP hiện hành (`ClientSession`), kết nối ngắt đồng nghĩa phiên kết thúc |

---

## 2. Đóng khung thông điệp (Message Framing)

**Cách chọn:** Mỗi thông điệp là **một chuỗi JSON nằm trọn vẹn trên đúng một dòng**, kết thúc bằng ký tự xuống dòng `\n` (0x0A).
- Phía gửi: Dùng `StreamWriter.WriteLineAsync(json)` kết hợp `AutoFlush = true`.
- Phía nhận: Dùng `StreamReader.ReadLineAsync()` để đọc chính xác từng thông điệp.

**Vì sao TCP bắt buộc phải có đóng khung:**
TCP là giao thức hướng luồng byte liên tục (byte stream), hoàn toàn không lưu giữ ranh giới giữa các lần gửi (`Write`). Do đó:
- Hiện tượng **dính gói (packet coalescing / Nagle algorithm)**: Hai thông điệp gửi liên tiếp có thể được gộp lại và bên nhận đọc được trong một lần `Read`.
- Hiện tượng **phân mảnh (packet fragmentation)**: Một thông điệp lớn có thể bị cắt thành nhiều mẩu truyền qua mạng.
Nếu không có cơ chế đóng khung xác định ranh giới (delimiter hoặc length-prefix), bên nhận không thể biết thông điệp bắt đầu và kết thúc ở đâu.

**Vì sao chọn giải pháp "JSON + ngắt dòng \n":**
1. **An toàn tuyệt đối:** Trình tuần tự hoá JSON (`System.Text.Json`) luôn tự động escape ký tự xuống dòng bên trong dữ liệu chuỗi thành `\n` (2 ký tự ASCII riêng biệt `\` và `n`). Do đó, byte `0x0A` **chỉ xuất hiện duy nhất ở cuối thông điệp**, người dùng nhập văn bản có dấu xuống dòng vẫn an toàn.
2. **Tối ưu và đơn giản:** Tận dụng trực tiếp `StreamReader.ReadLineAsync()` và `StreamWriter.WriteLineAsync()` chuẩn của .NET, không cần tự quản lý bộ đệm đếm byte phức tạp.
3. **Dễ quan sát, gỡ lỗi:** Toàn bộ thông điệp là văn bản rõ ràng, xem log thuận tiện và có thể kiểm thử trực tiếp bằng công cụ `netcat` (`nc 127.0.0.1 5050`).
4. **So với cách dùng tiền tố độ dài (Length-prefix 4 byte):** Cách phân tách theo dòng phù hợp lý tưởng với dữ liệu hướng thông điệp văn bản ngắn của bài toán, giữ cho mã nguồn trong sáng và dễ bảo trì.

**Giới hạn độ dài:** Mỗi thông điệp tối đa 65.536 ký tự (64 KB). Nếu vượt quá giới hạn, server chủ động ngắt kết nối để ngăn tấn công làm tràn bộ nhớ.

**Kiểm chứng:** Chương trình `UserMgmt.TestClient` mở nhiều kết nối đồng thời, mỗi client gửi liên tục 20 chuỗi chứa tiếng Việt và ký tự xuống dòng. Kết quả: toàn bộ thông điệp đều được nhận đầy đủ, không bị dính liền hay phân mảnh.

---

## 3. Cấu trúc thông điệp

### 3.1. Thông điệp yêu cầu (Request: Client → Server)
```json
{
  "cmd": "DANGNHAP",
  "data": {
    "username": "nguyenvana",
    "password": "Password@123"
  },
  "token": null
}
```
| Trường | Kiểu | Bắt buộc | Ý nghĩa |
|---|---|---|---|
| `cmd` | `string` | Có | Mã lệnh cần thực hiện (xem mục 4) |
| `data` | `Dictionary<string, string>` | Tuỳ lệnh | Các tham số truyền kèm của lệnh |
| `token` | `string` | Không | Token xác thực (dành cho phần mở rộng) |

### 3.2. Thông điệp phản hồi (Response: Server → Client)
```json
{
  "ok": true,
  "code": "OK",
  "message": "Đăng nhập thành công",
  "data": {
    "userId": "1",
    "username": "nguyenvana",
    "fullName": "Nguyễn Văn A",
    "email": "vana@example.com",
    "birthday": "2000-01-15"
  }
}
```
| Trường | Kiểu | Bắt buộc | Ý nghĩa |
|---|---|---|---|
| `ok` | `bool` | Có | `true` khi thực hiện thành công (`code` = `"OK"`), `false` khi có lỗi |
| `code` | `string` | Có | Mã kết quả trả về (xem mục 5) |
| `message` | `string` | Có | Thông báo tiếng Việt rõ ràng, thân thiện để Client hiển thị trực tiếp cho người dùng |
| `data` | `Dictionary<string, string>` | Tuỳ lệnh | Dữ liệu kèm theo kết quả phản hồi (ví dụ thông tin người dùng) |

---

## 4. Bảng mã lệnh

| Mã lệnh (`cmd`) | Tham số gửi kèm (`data`) | Phản hồi thành công | Các mã lỗi có thể trả về |
|---|---|---|---|
| `DANGKY` | `username`: tên đăng nhập (tối đa 50 ký tự)<br>`password`: mật khẩu nguyên bản<br>`email`: địa chỉ thư điện tử<br>`fullName`: họ và tên đầy đủ<br>`birthday`: ngày sinh (`yyyy-MM-dd`) | `OK` + thông báo thành công | `E_BAD_REQUEST`<br>`E_USER_EXISTS`<br>`E_SERVER` |
| `DANGNHAP` | `username`: tên đăng nhập<br>`password`: mật khẩu người dùng nhập | `OK` + `data` gồm: `userId`, `username`, `fullName`, `email`, `birthday` | `E_BAD_REQUEST`<br>`E_INVALID_CREDENTIALS`<br>`E_SERVER` |
| `LAYTHONGTIN` | *(Không cần)* – Server nhận diện phiên qua kết nối TCP | `OK` + `data` người dùng mới nhất từ CSDL | `E_NOT_LOGGED_IN`<br>`E_SERVER` |
| `DANGXUAT` | *(Không cần)* | `OK` + thông báo đăng xuất, sau đó server đóng kết nối | – |
| `PING` | `text`: chuỗi văn bản cần kiểm tra | `OK` + `text`: lặp lại nguyên văn chuỗi gửi | – |

**Lưu ý nghiệp vụ và bảo mật:**
1. **Băm mật khẩu ở Server:** Mật khẩu gửi từ Client lên Server là mật khẩu người dùng vừa nhập. Server thực hiện băm bằng thuật toán an toàn PBKDF2-HMAC-SHA256 kết hợp Salt ngẫu nhiên 16 byte riêng biệt cho từng tài khoản trước khi lưu vào CSDL.
2. **Không để lộ thông tin nhạy cảm:** Thông điệp phản hồi **tuyệt đối không bao giờ** chứa mật khẩu, chuỗi băm (`PasswordHash`) hay Salt (`PasswordSalt`).
3. **Chống dò tài khoản:** Khi đăng nhập thất bại, bất kể do sai tên đăng nhập hay sai mật khẩu, server đều trả về **cùng một mã lỗi** `E_INVALID_CREDENTIALS` và cùng thông báo `"Tên đăng nhập hoặc mật khẩu không đúng"`. Server còn thực hiện thêm phép băm giả lập (`VerifyDummy`) khi không tìm thấy tên tài khoản để triệt tiêu chênh lệch thời gian xử lý (chống tấn công kênh bên - timing attack).

---

## 5. Bảng mã lỗi

| Mã lỗi (`code`) | Ý nghĩa | Thông báo mẫu hiển thị |
|---|---|---|
| `OK` | Thao tác thành công | *(Tuỳ thuộc từng lệnh)* |
| `E_BAD_REQUEST` | Thông điệp không đúng định dạng JSON, thiếu `cmd` hoặc thiếu tham số bắt buộc | Yêu cầu không đúng định dạng. |
| `E_UNKNOWN_CMD` | Mã lệnh không tồn tại trong giao thức | Lệnh không được hỗ trợ. |
| `E_USER_EXISTS` | Tên đăng nhập đã được người khác sử dụng | Tên đăng nhập đã tồn tại. |
| `E_INVALID_CREDENTIALS` | Tên đăng nhập không tồn tại hoặc sai mật khẩu | Tên đăng nhập hoặc mật khẩu không đúng. |
| `E_NOT_LOGGED_IN` | Yêu cầu thao tác cần xác thực khi chưa đăng nhập | Bạn chưa đăng nhập. |
| `E_TOKEN_EXPIRED` | Phiên làm việc / token xác thực đã hết hạn | Phiên đăng nhập đã hết hạn. |
| `E_SERVER` | Lỗi nội bộ phía server (CSDL...). Chi tiết kỹ thuật chỉ ghi lại ở log server | Server gặp sự cố, vui lòng thử lại sau. |

---

## 6. Xử lý các tình huống ngắt kết nối (3 kiểu client rời đi)

Hệ thống xử lý đầy đủ ba tình huống client ngắt kết nối theo yêu cầu tiêu chí 8:

| Kiểu rời đi | Hành vi phía Client | Cách thức Server nhận biết & xử lý |
|---|---|---|
| **Kiểu 1: Rời đàng hoàng** *(Bấm nút Đăng xuất)* | Client gửi lệnh `DANGXUAT`, đợi phản hồi `OK`, rồi đóng socket kết nối. Form chính quay về Form Đăng nhập. | Server đọc được thông điệp `DANGXUAT`, trả lời `OK`, ghi log `"đăng xuất"`, gỡ phiên khỏi `ConcurrentDictionary` trong khối `finally` và đóng socket. |
| **Kiểu 2: Tắt cửa sổ** *(Bấm nút đỏ X)* | Sự kiện `FormClosing` đóng kết nối socket trực tiếp (`_connection.Dispose()`), không gửi lệnh. | Khi socket đóng từ client, lời gọi `ReadLineAsync()` tại server trả về giá trị `null` (EOF). Server ghi log `"client đóng kết nối"` và dọn dẹp phiên trong khối `finally`. |
| **Kiểu 3: Mất mạng đột ngột** *(Tắt Wi-Fi / rút cáp / crash)* | Client mất nguồn hoặc mất mạng đột ngột, không thể gửi tín hiệu đóng kết nối TCP. | Server bật tuỳ chọn TCP KeepAlive (thời gian im lặng 30s, thử lại 3 lần x 5s). Khi socket timeout hoặc nhận cờ RST, `ReadLineAsync()` ném ngoại lệ `IOException`/`SocketException`. Server bắt lỗi, ghi log `"mất kết nối đột ngột"` và dọn dẹp trong khối `finally`. |

**Đảm bảo an toàn luồng:** Server sử dụng danh sách `ConcurrentDictionary<Guid, ClientSession>` và `ConcurrentDictionary<Guid, Task>`. Mọi trường hợp thoát của client đều được loại bỏ chắc chắn khỏi danh sách trong khối `finally`.

---

## 7. Dừng server tử tế (Graceful Shutdown)

Khi người quản trị dừng server (nhấn `Ctrl+C` tại cửa sổ dòng lệnh):
1. Server bắt sự kiện `Console.CancelKeyPress`, kích hoạt huỷ thông qua `CancellationTokenSource` (`cts.Cancel()`).
2. Vòng lặp `listener.AcceptTcpClientAsync(ct)` ném `OperationCanceledException`; server bắt và bỏ qua để ngừng nhận kết nối mới.
3. Trong khối `finally`:
   - Duyệt qua toàn bộ danh sách `_clients` để đóng an toàn các socket client đang kết nối.
   - Cho phép các tác vụ xử lý dở dang có tối đa 5 giây để hoàn tất (`Task.WhenAny`).
   - Gọi `listener.Stop()` để giải phóng cổng TCP lắng nghe, tránh lỗi chiếm dụng cổng khi khởi động lại.
