namespace UserMgmt.Shared;

/// <summary>Một yêu cầu client gửi lên server. Mỗi Request là đúng một dòng JSON.</summary>
/// <param name="Cmd">Mã lệnh, xem <see cref="Commands"/>.</param>
/// <param name="Data">Tham số của lệnh, khoá xem <see cref="Fields"/>.</param>
/// <param name="Token">Dành cho phần nâng cao (token). Để null nếu không dùng.</param>
public record Request(string Cmd, Dictionary<string, string>? Data = null, string? Token = null)
{
    /// <summary>Lấy một tham số, trả null nếu thiếu hoặc rỗng.</summary>
    public string? Get(string field)
        => Data is not null && Data.TryGetValue(field, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
}

/// <summary>Phản hồi server gửi về client. Mỗi Response là đúng một dòng JSON.</summary>
/// <param name="Ok">true khi Code == "OK".</param>
/// <param name="Code">Mã kết quả, xem <see cref="ErrorCodes"/>.</param>
/// <param name="Message">Câu thông báo tiếng Việt, client hiển thị thẳng cho người dùng.</param>
/// <param name="Data">Dữ liệu trả về (thông tin người dùng, ...).</param>
public record Response(bool Ok, string Code, string Message, Dictionary<string, string>? Data = null)
{
    public static Response Success(string message, Dictionary<string, string>? data = null)
        => new(true, ErrorCodes.Ok, message, data);

    public static Response Fail(string code, string message)
        => new(false, code, message);
}
