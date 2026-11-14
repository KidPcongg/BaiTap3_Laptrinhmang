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
    /// DANGNHAP – Vào: username, password
    /// 1. Lấy người dùng theo tên (truy vấn có tham số), băm lại mật khẩu với salt đã lưu rồi so sánh
    /// 2. Sai tên HOẶC sai mật khẩu -> CÙNG MỘT mã E_INVALID_CREDENTIALS và cùng một câu thông báo
    ///    Sai tên vẫn tốn một lần băm (VerifyDummy) để thời gian phản hồi giống hệt sai mật khẩu
    /// 3. Đúng: gắn phiên đăng nhập vào kết nối này, trả thông tin người dùng (không có hash/salt)
    /// </summary>
    private async Task<Response> HandleDangNhapAsync(Request request, ClientSession session)
    {
        string? username = request.Get(Fields.Username)?.Trim();
        string? password = request.Get(Fields.Password);

        if (username is null || password is null)
            return BadRequest("Vui lòng nhập tên đăng nhập và mật khẩu");

        NguoiDung? user = await _repository.FindByUsernameAsync(username);
        bool passwordOk = user is null
            ? PasswordHasher.VerifyDummy(password)
            : PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt);

        if (user is null || !passwordOk)
        {
            // Log ghi rõ để dò lỗi, nhưng client chỉ nhận một câu chung cho cả hai trường hợp
            Logger.Warn($"{session} đăng nhập thất bại với tên '{username}' ({(user is null ? "không có tên này" : "sai mật khẩu")})");
            return Response.Fail(ErrorCodes.InvalidCredentials, "Tên đăng nhập hoặc mật khẩu không đúng");
        }

        session.UserId = user.UserId;
        session.Username = user.Username;
        Logger.Info($"{session} đăng nhập thành công");
        return Response.Success("Đăng nhập thành công", ToResponseData(user));
    }

    /// <summary>
    /// LAYTHONGTIN – không có tham số, dùng phiên đã đăng nhập trên chính kết nối này
    /// 1. Chưa đăng nhập -> E_NOT_LOGGED_IN
    /// 2. Đọc lại từ CSDL theo UserId để luôn trả thông tin mới nhất
    /// </summary>
    private async Task<Response> HandleLayThongTinAsync(ClientSession session)
    {
        if (session.UserId is not int userId)
            return Response.Fail(ErrorCodes.NotLoggedIn, "Bạn chưa đăng nhập");

        NguoiDung? user = await _repository.FindByIdAsync(userId);
        if (user is null)
        {
            // Tài khoản bị xoá khỏi CSDL trong lúc đang đăng nhập
            session.Logout();
            return Response.Fail(ErrorCodes.NotLoggedIn, "Tài khoản không còn tồn tại, vui lòng đăng nhập lại");
        }

        return Response.Success("Lấy thông tin thành công", ToResponseData(user));
    }

    /// <summary>
    /// Thông tin gửi cho client. Chỉ chọn từng trường cần thiết,
    /// KHÔNG BAO GIỜ đưa PasswordHash hay PasswordSalt vào đây
    /// </summary>
    private static Dictionary<string, string> ToResponseData(NguoiDung user) => new()
    {
        [Fields.UserId] = user.UserId.ToString(CultureInfo.InvariantCulture),
        [Fields.Username] = user.Username,
        [Fields.Email] = user.Email,
        [Fields.FullName] = user.FullName,
        [Fields.Birthday] = user.Birthday?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
    };
}