namespace UserMgmt.Server.Data;

/// <summary>
/// Một dòng của bảng NguoiDung. Chỉ dùng bên trong server:
/// PasswordHash và PasswordSalt KHÔNG BAO GIỜ được đưa vào Response gửi cho client.
/// </summary>
public sealed record NguoiDung(
    int UserId,
    string Username,
    byte[] PasswordHash,
    byte[] PasswordSalt,
    string Email,
    string FullName,
    DateTime? Birthday);