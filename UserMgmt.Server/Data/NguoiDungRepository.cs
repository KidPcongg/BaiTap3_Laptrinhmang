using System.Data;
using Microsoft.Data.SqlClient;

namespace UserMgmt.Server.Data;


public sealed class NguoiDungRepository
{
    private const string SelectColumns =
        "SELECT UserId, Username, PasswordHash, PasswordSalt, Email, FullName, Birthday FROM dbo.NguoiDung";

    private readonly string _connectionString;

    public NguoiDungRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>Thử mở kết nối một lần lúc server khởi động. Ném SqlException nếu không kết nối được</summary>
    public async Task CheckConnectionAsync()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT 1 FROM dbo.NguoiDung WHERE Username = @Username", conn);
        cmd.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = username;
        return await cmd.ExecuteScalarAsync() is not null;
    }

    /// <summary>Thêm người dùng mới, trả về UserId vừa sinh</summary>
    /// <exception cref="UsernameTakenException">Tên đăng nhập đã tồn tại (vi phạm ràng buộc UNIQUE)</exception>
    public async Task<int> InsertAsync(string username, byte[] passwordHash, byte[] passwordSalt,
                                       string email, string fullName, DateTime? birthday)
    {
        const string sql = """
            INSERT INTO dbo.NguoiDung (Username, PasswordHash, PasswordSalt, Email, FullName, Birthday)
            OUTPUT INSERTED.UserId
            VALUES (@Username, @PasswordHash, @PasswordSalt, @Email, @FullName, @Birthday)
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = username;
        cmd.Parameters.Add("@PasswordHash", SqlDbType.VarBinary, 32).Value = passwordHash;
        cmd.Parameters.Add("@PasswordSalt", SqlDbType.VarBinary, 16).Value = passwordSalt;
        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = email;
        cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 100).Value = fullName;
        cmd.Parameters.Add("@Birthday", SqlDbType.Date).Value = (object?)birthday ?? DBNull.Value;

        try
        {
            return (int)(await cmd.ExecuteScalarAsync())!;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            // 2627/2601 = vi phạm UNIQUE: hai client đăng ký cùng một tên gần như cùng lúc,
            // cả hai đều qua bước kiểm tra UsernameExistsAsync nhưng chỉ một INSERT thành công
            throw new UsernameTakenException(username, ex);
        }
    }

    public async Task<NguoiDung?> FindByUsernameAsync(string username)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(SelectColumns + " WHERE Username = @Username", conn);
        cmd.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = username;
        return await ReadSingleAsync(cmd);
    }

    public async Task<NguoiDung?> FindByIdAsync(int userId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(SelectColumns + " WHERE UserId = @UserId", conn);
        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        return await ReadSingleAsync(cmd);
    }

    private static async Task<NguoiDung?> ReadSingleAsync(SqlCommand cmd)
    {
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new NguoiDung(
            UserId: reader.GetInt32(0),
            Username: reader.GetString(1),
            PasswordHash: reader.GetFieldValue<byte[]>(2),
            PasswordSalt: reader.GetFieldValue<byte[]>(3),
            Email: reader.GetString(4),
            FullName: reader.GetString(5),
            Birthday: reader.IsDBNull(6) ? null : reader.GetDateTime(6));
    }
}

/// <summary>Ném ra khi INSERT bị trùng tên đăng nhập. Dispatcher bắt và trả mã E_USER_EXISTS</summary>
public sealed class UsernameTakenException(string username, Exception inner)
    : Exception($"Tên đăng nhập '{username}' đã tồn tại.", inner);