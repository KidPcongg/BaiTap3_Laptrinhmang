using UserMgmt.Server.Data;
using UserMgmt.Shared;

namespace UserMgmt.Server;

/// <summary>
/// Phân tích lệnh: nhận một Request, gọi hàm xử lý tương ứng, trả về Response.
///
/// HỢP ĐỒNG với vòng lặp phục vụ (TcpServer):
///  - Không bao giờ ném ngoại lệ ra ngoài vì lỗi nghiệp vụ; luôn trả Response.
///  - Lỗi CSDL (SqlException...) thì ghi Logger.Error rồi trả ErrorCodes.Server, KHÔNG đưa ex.Message cho client.
///  - DANGXUAT: chỉ cần trả Response; TcpServer sẽ tự đóng kết nối và gỡ khỏi danh sách.
///
/// PHẦN CỦA NGƯỜI 2: cài đặt 3 hàm có đánh dấu TODO bên dưới, dùng lớp truy cập CSDL của Bài 2.2
/// (truy vấn có tham số) và PasswordHasher.
/// </summary>
public sealed class Dispatcher
{
    private readonly NguoiDungRepository _repository;

    public Dispatcher(NguoiDungRepository repository)
    {
        _repository = repository;
    }
    public async Task<Response> HandleAsync(Request request, ClientSession session)
    {
        try
        {
            return request.Cmd switch
            {
                Commands.Ping => HandlePing(request),
                Commands.DangKy => await HandleDangKyAsync(request),
                Commands.DangNhap => await HandleDangNhapAsync(request, session),
                Commands.LayThongTin => await HandleLayThongTinAsync(session),
                Commands.DangXuat => HandleDangXuat(session),
                _ => Response.Fail(ErrorCodes.UnknownCommand, "Lệnh không được hỗ trợ."),
            };
        }
        catch (Exception ex)
        {
            // Lưới an toàn cuối cùng: ghi đầy đủ ở server, client chỉ nhận thông báo chung.
            Logger.Error($"{session} lỗi khi xử lý {request.Cmd}: {ex}");
            return Response.Fail(ErrorCodes.Server, "Server gặp sự cố, vui lòng thử lại sau.");
        }
    }

    private static Response HandlePing(Request request)
        => Response.Success("PONG", new() { [Fields.Text] = request.Get(Fields.Text) ?? "" });

    private static Response HandleDangXuat(ClientSession session)
    {
        session.Logout();
        return Response.Success("Đã đăng xuất.");
    }

    // ===================== TODO: NGƯỜI 2 =====================

    /// <summary>
    /// Vào: username, password, email, fullName, birthday (yyyy-MM-dd).
    /// 1. Thiếu tham số -> E_BAD_REQUEST.
    /// 2. SELECT 1 FROM NguoiDung WHERE Username = @Username  (có tham số) -> đã có thì E_USER_EXISTS.
    /// 3. PasswordHasher.Hash(password) -> INSERT NguoiDung (...) VALUES (@...).
    ///    Bắt thêm SqlException số 2627/2601 (vi phạm UNIQUE) -> E_USER_EXISTS, phòng 2 client đăng ký cùng lúc.
    /// 4. Trả Response.Success("Đăng ký thành công.").
    /// </summary>
    private Task<Response> HandleDangKyAsync(Request request)
        => Task.FromResult(NotImplemented(Commands.DangKy));

    /// <summary>
    /// Vào: username, password.
    /// 1. SELECT UserId, Username, PasswordHash, PasswordSalt, Email, FullName, Birthday WHERE Username = @Username.
    /// 2. Không có user HOẶC sai mật khẩu -> CÙNG MỘT câu: E_INVALID_CREDENTIALS "Tên đăng nhập hoặc mật khẩu không đúng."
    /// 3. Đúng: session.UserId = ..., session.Username = ...; trả Data gồm userId, username, email, fullName, birthday.
    ///    KHÔNG BAO GIỜ đưa PasswordHash/PasswordSalt vào Data.
    /// </summary>
    private Task<Response> HandleDangNhapAsync(Request request, ClientSession session)
        => Task.FromResult(NotImplemented(Commands.DangNhap));

    /// <summary>
    /// 1. !session.IsLoggedIn -> E_NOT_LOGGED_IN.
    /// 2. SELECT ... WHERE UserId = @UserId -> trả Data như DANGNHAP.
    /// </summary>
    private Task<Response> HandleLayThongTinAsync(ClientSession session)
        => Task.FromResult(NotImplemented(Commands.LayThongTin));

    private static Response NotImplemented(string cmd)
    {
        Logger.Warn($"Lệnh {cmd} chưa được cài đặt.");
        return Response.Fail(ErrorCodes.Server, "Chức năng đang được hoàn thiện.");
    }
}
