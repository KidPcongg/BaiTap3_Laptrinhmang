using System.Globalization;
using System.Net.Sockets;
using UserMgmt.Shared;

namespace UserMgmt.Client;

public partial class TrangChinhForm : Form
{
    private readonly ServerConnection _connection;
    private bool _busy;
    private bool _daNgatKetNoi;
    private readonly DateTime _loginTime = DateTime.Now;

    public bool IsLogout { get; private set; }
    public string StatusMessage { get; private set; } = "Đã đăng xuất";

    public TrangChinhForm(ServerConnection connection, Dictionary<string, string> initialData)
    {
        _connection = connection;
        InitializeComponent();
        FormStyle.Apply(this);

        DisplayUserData(initialData);

        _connection.StatusChanged += msg =>
        {
            if (!IsDisposed && IsHandleCreated)
            {
                BeginInvoke(() =>
                {
                    lblTrangThai.Text = msg;
                    lblTrangThai.ForeColor = _connection.IsConnected ? FormStyle.Teal : FormStyle.Error;
                });
            }
        };
    }

    private void DisplayUserData(Dictionary<string, string> data)
    {
        string fullName = data.GetValueOrDefault(Fields.FullName, "");
        string username = data.GetValueOrDefault(Fields.Username, "");
        string email = data.GetValueOrDefault(Fields.Email, "");
        string birthday = data.GetValueOrDefault(Fields.Birthday, "");

        lblXinChao.Text = $"Xin chào, {fullName}";
        lblTenDangNhapValue.Text = username;
        lblHoTenValue.Text = fullName;
        lblEmailValue.Text = string.IsNullOrWhiteSpace(email) ? "Chưa cung cấp" : email;

        if (DateTime.TryParseExact(birthday, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            lblNgaySinhValue.Text = dt.ToString("dd/MM/yyyy");
        else
            lblNgaySinhValue.Text = string.IsNullOrWhiteSpace(birthday) ? "Chưa cung cấp" : birthday;

        lblThoiGianDangNhapValue.Text = _loginTime.ToString("dd/MM/yyyy HH:mm:ss");

        lblTrangThai.Text = _connection.IsConnected ? "Đã kết nối server" : "Mất kết nối tới server";
        lblTrangThai.ForeColor = _connection.IsConnected ? FormStyle.Teal : FormStyle.Error;
    }

    private async void btnTaiLai_Click(object? sender, EventArgs e)
    {
        if (_busy) return;
        SetBusy(true);
        lblThongBao.Text = "";

        try
        {
            Response resp = await _connection.SendAsync(new Request(Commands.LayThongTin));
            if (resp.Ok && resp.Data is not null)
            {
                DisplayUserData(resp.Data);
                lblThongBao.ForeColor = FormStyle.Teal;
                lblThongBao.Text = "Đã làm mới thông tin thành công!";
            }
            else
            {
                lblThongBao.ForeColor = FormStyle.Error;
                lblThongBao.Text = resp.Message;
                if (resp.Code == ErrorCodes.NotLoggedIn)
                {
                    MessageBox.Show(this, "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    StatusMessage = resp.Message;
                    IsLogout = true;
                    Close();
                }
            }
        }
        catch (SocketException)
        {
            ShowError("Mất kết nối tới server. Vui lòng kiểm tra lại mạng.");
        }
        catch (IOException)
        {
            ShowError("Mất kết nối tới server.");
        }
        catch (TimeoutException)
        {
            ShowError("Server không phản hồi kịp thời.");
        }
        catch (Exception)
        {
            ShowError("Đã xảy ra lỗi khi tải lại dữ liệu. Vui lòng thử lại sau.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void btnDangXuat_Click(object? sender, EventArgs e)
    {
        if (_busy) return;
        SetBusy(true);

        _daNgatKetNoi = true;
        IsLogout = true;
        StatusMessage = "Đã đăng xuất thành công.";
        DialogResult = DialogResult.OK;

        try
        {
            // Kiểu 1: Rời đàng hoàng -> Gửi DANGXUAT, nhận phản hồi rồi đóng kết nối
            await _connection.LogoutAndCloseAsync();
        }
        catch
        {
            // Bỏ qua lỗi ngắt kết nối
        }
        finally
        {
            SetBusy(false);
        }

        Close();
    }

    private void TrangChinhForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_daNgatKetNoi)
            return;

        _daNgatKetNoi = true;
        IsLogout = false;
        StatusMessage = "Đã đóng kết nối";
        DialogResult = DialogResult.Cancel;

        try
        {
            // Kiểu 2: Tắt cửa sổ bằng nút đỏ -> Đóng socket trực tiếp (Dispose/Close)
            // Không gửi DANGXUAT để server phát hiện đóng kết nối (ReadLine trả về null)
            _connection.Dispose();
        }
        catch
        {
            // Bỏ qua lỗi ngắt kết nối
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        btnTaiLai.Enabled = !busy;
        btnDangXuat.Enabled = !busy;
        btnTaiLai.Text = busy ? "Đang xử lý..." : "Tải lại thông tin";
        btnDangXuat.Text = busy ? "Đang xử lý..." : "Đăng xuất";
    }

    private void ShowError(string message)
    {
        lblThongBao.ForeColor = FormStyle.Error;
        lblThongBao.Text = message;
    }
}
