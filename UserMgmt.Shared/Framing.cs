using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UserMgmt.Shared;

/// <summary>
/// Đóng khung thông điệp: mỗi thông điệp = một object JSON trên MỘT dòng, kết thúc bằng '\n',
/// mã hoá UTF-8 không BOM.
///
/// Vì sao an toàn: JsonSerializer luôn escape ký tự xuống dòng trong chuỗi thành "\n" (2 ký tự),
/// nên byte 0x0A chỉ xuất hiện ở cuối thông điệp, không bao giờ nằm trong nội dung.
/// </summary>
public static class Framing
{
    /// <summary>
    /// BẮT BUỘC dùng encoding này cho cả StreamReader và StreamWriter ở hai phía.
    /// Encoding.UTF8 mặc định sẽ ghi 3 byte BOM ở đầu luồng, làm hỏng dòng JSON đầu tiên.
    /// </summary>
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Giới hạn độ dài một thông điệp (ký tự) để chặn dòng rác quá dài.</summary>
    public const int MaxMessageLength = 64 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Giữ nguyên chữ có dấu (UTF-8) thay vì ê... cho log dễ đọc.
        // Ký tự điều khiển như '\n' VẪN luôn bị escape, nên đóng khung theo dòng vẫn an toàn.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static StreamReader CreateReader(Stream stream)
        => new(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);

    public static StreamWriter CreateWriter(Stream stream)
        => new(stream, Utf8NoBom, bufferSize: 4096, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };

    public static string Serialize<T>(T message) => JsonSerializer.Serialize(message, JsonOptions);

    /// <summary>Ném JsonException nếu dòng không phải JSON hợp lệ.</summary>
    public static T? Deserialize<T>(string line) => JsonSerializer.Deserialize<T>(line, JsonOptions);

    /// <summary>Ghi một thông điệp (một dòng) rồi đẩy bộ đệm.</summary>
    public static async Task WriteAsync<T>(StreamWriter writer, T message, CancellationToken ct = default)
    {
        await writer.WriteLineAsync(Serialize(message).AsMemory(), ct);
        await writer.FlushAsync(ct);
    }

    /// <summary>Đọc một dòng thô. Trả null khi bên kia đã đóng kết nối.</summary>
    public static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken ct = default)
    {
        var line = await reader.ReadLineAsync(ct);
        if (line is not null && line.Length > MaxMessageLength)
            throw new InvalidDataException($"Thông điệp dài {line.Length} ký tự, vượt giới hạn {MaxMessageLength}.");
        return line;
    }

    /// <summary>Đọc và giải mã một thông điệp. Trả default khi bên kia đã đóng kết nối.</summary>
    public static async Task<T?> ReadAsync<T>(StreamReader reader, CancellationToken ct = default)
    {
        var line = await ReadLineAsync(reader, ct);
        return line is null ? default : Deserialize<T>(line);
    }
}
