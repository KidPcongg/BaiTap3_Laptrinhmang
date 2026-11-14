using System.Security.Cryptography;
using System.Text;

namespace UserMgmt.Server.Security;

/// <summary>
/// Băm mật khẩu bằng PBKDF2-HMAC-SHA256 (Rfc2898DeriveBytes.Pbkdf2), theo mục III.7 của đề.
///  - Mỗi tài khoản một salt 16 byte sinh ngẫu nhiên bằng RandomNumberGenerator,
///    nên hai tài khoản cùng mật khẩu vẫn ra hai chuỗi băm khác nhau
///  - 100 000 vòng lặp: đủ chậm để chống dò mật khẩu, mỗi lần băm khoảng vài chục ms.
/// </summary>
public static class PasswordHasher
{
    public const int SaltSize = 16;          // byte, khớp cột PasswordSalt VARBINARY(16)
    public const int HashSize = 32;          // byte, khớp cột PasswordHash VARBINARY(32)
    public const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>Dùng khi tên đăng nhập không tồn tại, để thời gian phản hồi giống hệt trường hợp sai mật khẩu.</summary>
    private static readonly byte[] DummySalt = RandomNumberGenerator.GetBytes(SaltSize);
    private static readonly byte[] DummyHash = Derive("dummy", DummySalt);

    /// <summary>Sinh salt mới và băm mật khẩu.</summary>
    public static (byte[] Hash, byte[] Salt) Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        return (Derive(password, salt), salt);
    }

    /// <summary>Băm lại mật khẩu với salt đã lưu rồi so sánh trong thời gian cố định.</summary>
    public static bool Verify(string password, byte[] hash, byte[] salt)
        => CryptographicOperations.FixedTimeEquals(Derive(password, salt), hash);

    /// <summary>
    /// Tốn đúng một lần băm như Verify nhưng luôn trả false. Gọi khi không tìm thấy tên đăng nhập
    /// để kẻ tấn công không đoán được tên nào tồn tại dựa vào thời gian phản hồi.
    /// </summary>
    public static bool VerifyDummy(string password)
    {
        Verify(password, DummyHash, DummySalt);
        return false;
    }

    private static byte[] Derive(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, HashSize);
}