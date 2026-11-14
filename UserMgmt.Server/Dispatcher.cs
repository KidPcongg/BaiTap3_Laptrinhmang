using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using UserMgmt.Server.Data;
using UserMgmt.Server.Security;
using UserMgmt.Shared;

namespace UserMgmt.Server;

/// <summary>
/// Phân tích lệnh: nhận một Request, gọi hàm xử lý tương ứng, trả về Response
///
/// HỢP ĐỒNG với vòng lặp phục vụ (TcpServer):
///  - Không bao giờ ném ngoại lệ ra ngoài vì lỗi nghiệp vụ; luôn trả Response
///  - Lỗi CSDL (SqlException...) thì ghi Logger.Error rồi trả ErrorCodes.Server, KHÔNG đưa ex.Message cho client
///  - DANGXUAT: chỉ cần trả Response; TcpServer sẽ tự đóng kết nối và gỡ khỏi danh sách
///
/// Xử lý DANGKY, DANGNHAP, LAYTHONGTIN, lớp CSDL (Data/) và PasswordHasher (Security/): Dương Lê Tấn Dũng
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
                _ => Response.Fail(ErrorCodes.UnknownCommand, "Lệnh không được hỗ trợ"),
            };
        }
        catch (SqlException ex)
        {
            // Lỗi CSDL (mất kết nối SQL Server, sai chuỗi kết nối...): chi tiết chỉ ghi ở log server
            Logger.Error($"{session} lỗi CSDL khi xử lý {request.Cmd}: [{ex.Number}] {ex.Message}");
            return Response.Fail(ErrorCodes.Server, "Server gặp sự cố, vui lòng thử lại sau");
        }
        catch (Exception ex)
        {
            // Lưới an toàn cuối cùng: ghi đầy đủ ở server, client chỉ nhận thông báo chung
            Logger.Error($"{session} lỗi khi xử lý {request.Cmd}: {ex}");
            return Response.Fail(ErrorCodes.Server, "Server gặp sự cố, vui lòng thử lại sau");
        }
    }

    private static Response HandlePing(Request request)
        => Response.Success("PONG", new() { [Fields.Text] = request.Get(Fields.Text) ?? "" });

    private static Response HandleDangXuat(ClientSession session)
    {
        session.Logout();
        return Response.Success("Đã đăng xuất");
    }

    // ============ DANGKY / DANGNHAP / LAYTHONGTIN (Dũng) ============

    /// <summary>
    /// DANGKY – Vào: username, password, email, fullName, birthday (yyyy-MM-dd)
    /// 1. Kiểm tra lại dữ liệu ở server (client đã kiểm tra, nhưng server không tin client) -> E_BAD_REQUEST
    /// 2. Tên đăng nhập đã có (truy vấn có tham số) -> E_USER_EXISTS
    /// 3. Băm mật khẩu bằng PasswordHasher ngay trước khi INSERT
    ///    Hai client đăng ký cùng tên cùng lúc: INSERT thứ hai vi phạm UNIQUE -> cũng trả E_USER_EXISTS
    /// </summary>
    private async Task<Response> HandleDangKyAsync(Request request)
    {
        string? username = request.Get(Fields.Username)?.Trim();
        string? password = request.Get(Fields.Password);          // mật khẩu giữ nguyên, không Trim
        string? email = request.Get(Fields.Email)?.Trim();
        string? fullName = request.Get(Fields.FullName)?.Trim();
        string? birthdayText = request.Get(Fields.Birthday)?.Trim();

        if (username is null || password is null || email is null || fullName is null || birthdayText is null)
            return BadRequest("Vui lòng nhập đầy đủ thông tin");
        if (username.Length > 50)
            return BadRequest("Tên đăng nhập tối đa 50 ký tự");
        if (fullName.Length > 100)
            return BadRequest("Họ tên tối đa 100 ký tự");
        if (email.Length > 100 || !EmailPattern.IsMatch(email))
            return BadRequest("Email không đúng định dạng");
        if (!DateTime.TryParseExact(birthdayText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                    DateTimeStyles.None, out DateTime birthday)
            || birthday > DateTime.Today || birthday.Year < 1900)
            return BadRequest("Ngày sinh không hợp lệ");

        if (await _repository.UsernameExistsAsync(username))
            return UserExists();

        var (hash, salt) = PasswordHasher.Hash(password);
        try
        {
            int userId = await _repository.InsertAsync(username, hash, salt, email, fullName, birthday);
            Logger.Info($"Tạo tài khoản mới '{username}' (UserId={userId})");
        }
        catch (UsernameTakenException)
        {
            return UserExists();
        }

        return Response.Success("Đăng ký thành công");
    }

    /// <summary>Kiểm tra email dạng ten@mien.duoi, giống phần kiểm tra ở form Đăng ký của client</summary>
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private static Response BadRequest(string message) => Response.Fail(ErrorCodes.BadRequest, message);

    private static Response UserExists() => Response.Fail(ErrorCodes.UserExists, "Tên đăng nhập đã tồn tại");

    /// <summary>
    /// Vào: username, password
    /// 1. SELECT UserId, Username, PasswordHash, PasswordSalt, Email, FullName, Birthday WHERE Username = @Username
    /// 2. Không có user HOẶC sai mật khẩu -> CÙNG MỘT câu: E_INVALID_CREDENTIALS "Tên đăng nhập hoặc mật khẩu không đúng"
    /// 3. Đúng: session.UserId = ..., session.Username = ...; trả Data gồm userId, username, email, fullName, birthday
    ///    KHÔNG BAO GIỜ đưa PasswordHash/PasswordSalt vào Data
    /// </summary>
    private Task<Response> HandleDangNhapAsync(Request request, ClientSession session)
        => Task.FromResult(NotImplemented(Commands.DangNhap));

    /// <summary>
    /// 1. !session.IsLoggedIn -> E_NOT_LOGGED_IN
    /// 2. SELECT ... WHERE UserId = @UserId -> trả Data như DANGNHAP
    /// </summary>
    private Task<Response> HandleLayThongTinAsync(ClientSession session)
        => Task.FromResult(NotImplemented(Commands.LayThongTin));

    private static Response NotImplemented(string cmd)
    {
        Logger.Warn($"Lệnh {cmd} chưa được cài đặt");
        return Response.Fail(ErrorCodes.Server, "Chức năng đang được hoàn thiện");
    }
}