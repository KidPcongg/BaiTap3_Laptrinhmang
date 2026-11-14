# TÀI LIỆU GIAO THỨC – Ứng dụng quản lý người dùng (Bài tập 3)

## 1. Thông số kết nối

| Mục | Giá trị |
|---|---|
| Giao thức vận chuyển | TCP |
| Cổng server | **5050** (trong dải 1024–49151; đổi được bằng tham số dòng lệnh) |
| Địa chỉ nghe | `0.0.0.0` (`IPAddress.Any`) – máy khác trong mạng LAN nối vào được |
| Bảng mã | **UTF-8 không BOM**, cả hai phía |
| Mô hình | Yêu cầu – phản hồi: client gửi 1 Request, server trả đúng 1 Response, theo thứ tự |

## 2. Đóng khung thông điệp

**Cách chọn:** mỗi thông điệp là **một object JSON nằm trên đúng một dòng**, kết thúc bằng ký tự `\n` (0x0A).
Bên nhận đọc bằng `StreamReader.ReadLineAsync()`: mỗi lần đọc trả về đúng một thông điệp.

**Vì sao phải đóng khung:** TCP là luồng byte, không giữ ranh giới giữa các lần gửi. Hai lần `Write` có thể
đến bên kia trong một lần `Read` (dính liền), và một lần `Write` lớn có thể bị tách thành nhiều lần `Read`.
Nếu không có quy ước ranh giới, bên nhận không biết thông điệp kết thúc ở đâu.

**Vì sao chọn "JSON + xuống dòng":**
- Bộ tuần tự hoá JSON luôn chuyển ký tự xuống dòng trong chuỗi thành 2 ký tự `\` `n`, nên byte 0x0A
  **không bao giờ** xuất hiện bên trong nội dung, chỉ có ở cuối thông điệp. Người dùng gõ xuống dòng
  trong ô nhập vẫn an toàn.
- Dùng được thẳng `StreamReader`/`StreamWriter` của .NET, không cần tự đếm byte.
- Dễ quan sát và gỡ lỗi (đọc được bằng mắt trong log, thử được bằng `nc`).
- So với cách tiền tố độ dài (4 byte độ dài + nội dung): cách đó không phụ thuộc nội dung nhưng phải tự đọc
  cho đủ số byte và khó quan sát hơn; với thông điệp văn bản ngắn như bài này, phân tách theo dòng là đủ.

**Giới hạn:** một thông điệp tối đa 65 536 ký tự; dài hơn thì server đóng kết nối.

**Kiểm chứng:** chương trình `UserMgmt.TestClient` mở nhiều client, mỗi client gửi liên tiếp 20 chuỗi
(nội dung có tiếng Việt và ký tự xuống dòng) rồi mới đọc. Kết quả: mọi client nhận đủ 20/20 phản hồi đúng
thứ tự, không có chuỗi dính liền.

## 3. Cấu trúc thông điệp

**Request (client → server)**
```json
{"cmd":"DANGNHAP","data":{"username":"cong","password":"Abc@1234"}}
```
| Trường | Kiểu | Ý nghĩa |
|---|---|---|
| `cmd` | chuỗi | Mã lệnh (mục 4) |
| `data` | object chuỗi→chuỗi | Tham số của lệnh (có thể bỏ) |
| `token` | chuỗi | Chỉ dùng cho phần nâng cao (có thể bỏ) |

**Response (server → client)**
```json
{"ok":true,"code":"OK","message":"Đăng nhập thành công.","data":{"userId":"1","username":"cong", "...": "..."}}
```
| Trường | Kiểu | Ý nghĩa |
|---|---|---|
| `ok` | bool | `true` khi `code` = `OK` |
| `code` | chuỗi | Mã kết quả (mục 5) |
| `message` | chuỗi | Câu thông báo tiếng Việt, client hiển thị thẳng cho người dùng |
| `data` | object chuỗi→chuỗi | Dữ liệu trả về (có thể không có) |

## 4. Bảng mã lệnh

| Lệnh | Tham số (`data`) | Phản hồi thành công | Mã lỗi có thể trả |
|---|---|---|---|
| `DANGKY` | `username`, `password`, `email`, `fullName`, `birthday` (yyyy-MM-dd) | `OK` | `E_BAD_REQUEST`, `E_USER_EXISTS`, `E_SERVER` |
| `DANGNHAP` | `username`, `password` | `OK` + `userId`, `username`, `email`, `fullName`, `birthday` | `E_BAD_REQUEST`, `E_INVALID_CREDENTIALS`, `E_SERVER` |
| `LAYTHONGTIN` | (không) – dùng phiên đã đăng nhập trên kết nối này | `OK` + như DANGNHAP | `E_NOT_LOGGED_IN`, `E_SERVER` |
| `DANGXUAT` | (không) | `OK`, sau đó **server đóng kết nối** | – |
| `PING` | `text` | `OK` + `text` (lặp lại nguyên văn) | – |

Ghi chú:
- Mật khẩu gửi dạng thô; server băm bằng PBKDF2 trước khi lưu. Phản hồi **không bao giờ** chứa mật khẩu, chuỗi băm hay salt.
- Đăng nhập sai tên hay sai mật khẩu đều trả **cùng một** mã `E_INVALID_CREDENTIALS` và cùng một câu thông báo.
- Phiên đăng nhập gắn với kết nối TCP: đóng kết nối là mất phiên.

## 5. Bảng mã lỗi

| Mã | Ý nghĩa | Thông báo mẫu |
|---|---|---|
| `OK` | Thành công | (tuỳ lệnh) |
| `E_BAD_REQUEST` | Thông điệp không phải JSON hợp lệ, thiếu `cmd`, hoặc thiếu tham số bắt buộc | Yêu cầu không đúng định dạng. |
| `E_UNKNOWN_CMD` | Mã lệnh không có trong bảng mục 4 | Lệnh không được hỗ trợ. |
| `E_USER_EXISTS` | Tên đăng nhập đã được dùng | Tên đăng nhập đã tồn tại. |
| `E_INVALID_CREDENTIALS` | Sai tên đăng nhập hoặc mật khẩu | Tên đăng nhập hoặc mật khẩu không đúng. |
| `E_NOT_LOGGED_IN` | Gọi lệnh cần đăng nhập khi chưa đăng nhập | Bạn chưa đăng nhập. |
| `E_TOKEN_EXPIRED` | (Nâng cao) token hết hạn hoặc không hợp lệ | Phiên đăng nhập đã hết hạn. |
| `E_SERVER` | Lỗi nội bộ server (CSDL...). Chi tiết chỉ ghi ở log server | Server gặp sự cố, vui lòng thử lại sau. |

## 6. Xử lý ngắt kết nối

| Tình huống | Client làm gì | Server nhận biết bằng |
|---|---|---|
| Bấm Đăng xuất | Gửi `DANGXUAT`, nhận `OK`, đóng socket | Lệnh `DANGXUAT` |
| Tắt cửa sổ (nút đỏ) | `FormClosing`: gửi `DANGXUAT` nếu được, rồi đóng socket | `ReadLine` trả `null` |
| Mất mạng đột ngột | – | TCP keepalive (30s im lặng, thử 3 lần × 5s) → `IOException` |

Cả ba trường hợp server đều gỡ client khỏi danh sách trong khối `finally` và ghi log.
