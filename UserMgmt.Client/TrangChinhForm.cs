using System.Net.Sockets;
using UserMgmt.Shared;

namespace UserMgmt.Client;

public partial class TrangChinhForm : Form
{
    private readonly ServerConnection _connection;
    private bool _busy;
    private bool _daNgatKetNoi;
    private readonly DateTime _loginTime = DateTime.Now;

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
                    Close();
                }
            }
        }
        catch (SocketException)
        {
            ShowError("Mất kết nối tới server. Vui lòng thử lại.");
        }
        catch (IOException)
        {
            ShowError("Mất kết nối tới server.");
        }
        catch (TimeoutException)
        {
            ShowError("Server không phản hồi.");
        }
        catch (Exception ex)
        {
            ShowError($"Lỗi: {ex.Message}");
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
        StatusMessage = "Đã đăng xuất";
        DialogResult = DialogResult.OK;

        try
        {
            await _connection.LogoutAndCloseAsync();
        }
        catch
        {
            // Bỏ qua lỗi ngắt kết nối
        }

        Close();
    }

    private async void TrangChinhForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_daNgatKetNoi)
            return;

        e.Cancel = true;
        _daNgatKetNoi = true;
        StatusMessage = "Đã đóng kết nối";
        DialogResult = DialogResult.OK;

        try
        {
            await _connection.LogoutAndCloseAsync();
        }
        catch
        {
            // Bỏ qua lỗi ngắt kết nối
        }

        Close();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        btnTaiLai.Enabled = !busy;
        btnDangXuat.Enabled = !busy;
    }

    private void ShowError(string message)
    {
        lblThongBao.ForeColor = FormStyle.Error;
        lblThongBao.Text = message;
    }
}
