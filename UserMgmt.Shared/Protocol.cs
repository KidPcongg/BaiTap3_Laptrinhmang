namespace UserMgmt.Shared;

/// <summary>Thông số chung của giao thức. Xem docs/GiaoThuc.md.</summary>
public static class Protocol
{
    /// <summary>Cổng TCP mặc định của server (dải 1024–49151).</summary>
    public const int DefaultPort = 5050;
}

/// <summary>Mã lệnh client gửi lên server.</summary>
public static class Commands
{
    public const string DangKy = "DANGKY";
    public const string DangNhap = "DANGNHAP";
    public const string LayThongTin = "LAYTHONGTIN";
    public const string DangXuat = "DANGXUAT";
    public const string Ping = "PING";
}

/// <summary>Mã kết quả server trả về trong Response.Code.</summary>
public static class ErrorCodes
{
    public const string Ok = "OK";
    public const string BadRequest = "E_BAD_REQUEST";
    public const string UnknownCommand = "E_UNKNOWN_CMD";
    public const string UserExists = "E_USER_EXISTS";
    public const string InvalidCredentials = "E_INVALID_CREDENTIALS";
    public const string NotLoggedIn = "E_NOT_LOGGED_IN";
    public const string TokenExpired = "E_TOKEN_EXPIRED";
    public const string Server = "E_SERVER";
}

/// <summary>Tên các khoá trong Request.Data / Response.Data. Hai bên dùng chung để không gõ sai.</summary>
public static class Fields
{
    public const string UserId = "userId";
    public const string Username = "username";
    public const string Password = "password";
    public const string Email = "email";
    public const string FullName = "fullName";
    public const string Birthday = "birthday";   // định dạng yyyy-MM-dd
    public const string Text = "text";           // dùng cho PING
}
