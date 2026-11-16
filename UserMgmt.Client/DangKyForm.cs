using System.Net.Sockets;
using System.Text.RegularExpressions;
using UserMgmt.Shared;

namespace UserMgmt.Client;

public partial class DangKyForm : Form
{
    private readonly ServerConnection _connection;
    private readonly string _host;
    private readonly int _port;
    private bool _busy;

    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public string? CreatedUsername { get; private set; }

    public DangKyForm(ServerConnection connection, string host, int port)
    {
        _connection = connection;
        _host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
        _port = port;

        InitializeComponent();
        FormStyle.Apply(this);

        dtpNgaySinh.MaxDate = DateTime.Today;
        dtpNgaySinh.Value = DateTime.Today.AddYears(-18);

        UpdateConnectionStatus();
        _connection.StatusChanged += _ =>
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(UpdateConnectionStatus);
        };
    }

    private void UpdateConnectionStatus()
    {
        lblTrangThai.Text = _connection.IsConnected ? "Đã kết nối server" : "Chưa kết nối server";
        lblTrangThai.ForeColor = _connection.IsConnected ? FormStyle.Teal : FormStyle.Error;
    }

    private async void btnDangKy_Click(object? sender, EventArgs e)
    {
        if (_busy) return;
        ClearErrors();

        string username = txtTenDangNhap.Text.Trim();
        string password = txtMatKhau.Text;
        string confirmPassword = txtXacNhanMatKhau.Text;
        string email = txtEmail.Text.Trim();
        string fullName = txtHoTen.Text.Trim();
        DateTime birthday = dtpNgaySinh.Value.Date;

        bool hasError = false;

        if (string.IsNullOrWhiteSpace(username))
        {
            lblLoiTenDangNhap.Text = "Vui lòng nhập tên đăng nhập.";
            hasError = true;
        }
        else if (username.Length > 50)
        {
            lblLoiTenDangNhap.Text = "Tên đăng nhập tối đa 50 ký tự.";
            hasError = true;
        }

        if (string.IsNullOrEmpty(password))
        {
            lblLoiMatKhau.Text = "Vui lòng nhập mật khẩu.";
            hasError = true;
        }
        else if (password.Length < 6)
        {
            lblLoiMatKhau.Text = "Mật khẩu phải có ít nhất 6 ký tự.";
            hasError = true;
        }

        if (password != confirmPassword)
        {
            lblLoiXacNhanMatKhau.Text = "Mật khẩu xác nhận không khớp.";
            hasError = true;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            lblLoiEmail.Text = "Vui lòng nhập email.";
            hasError = true;
        }
        else if (email.Length > 100 || !EmailPattern.IsMatch(email))
        {
            lblLoiEmail.Text = "Email không đúng định dạng (ten@mien.duoi).";
            hasError = true;
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            lblLoiHoTen.Text = "Vui lòng nhập họ và tên.";
            hasError = true;
        }
        else if (fullName.Length > 100)
        {
            lblLoiHoTen.Text = "Họ tên tối đa 100 ký tự.";
            hasError = true;
        }

        if (birthday > DateTime.Today || birthday.Year < 1900)
        {
            lblLoiNgaySinh.Text = "Ngày sinh không hợp lệ.";
            hasError = true;
        }

        if (hasError)
        {
            if (!string.IsNullOrEmpty(lblLoiTenDangNhap.Text)) txtTenDangNhap.Focus();
            else if (!string.IsNullOrEmpty(lblLoiMatKhau.Text)) txtMatKhau.Focus();
            else if (!string.IsNullOrEmpty(lblLoiXacNhanMatKhau.Text)) txtXacNhanMatKhau.Focus();
            else if (!string.IsNullOrEmpty(lblLoiEmail.Text)) txtEmail.Focus();
            else if (!string.IsNullOrEmpty(lblLoiHoTen.Text)) txtHoTen.Focus();
            return;
        }

        SetBusy(true, "Đang gửi đăng ký...");
        try
        {
            if (!_connection.IsConnected)
                await _connection.ConnectAsync(_host, _port);

            var request = new Request(Commands.DangKy, new()
            {
                [Fields.Username] = username,
                [Fields.Password] = password,
                [Fields.Email] = email,
                [Fields.FullName] = fullName,
                [Fields.Birthday] = birthday.ToString("yyyy-MM-dd")
            });

            Response resp = await _connection.SendAsync(request);

            if (resp.Ok)
            {
                CreatedUsername = username;
                MessageBox.Show(this, "Đăng ký tài khoản thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                lblThongBao.ForeColor = FormStyle.Error;
                if (resp.Code == ErrorCodes.UserExists)
                {
                    lblLoiTenDangNhap.Text = resp.Message;
                    txtTenDangNhap.Focus();
                }
                else
                {
                    lblThongBao.Text = resp.Message;
                }
            }
        }
        catch (SocketException)
        {
            ShowError("Không kết nối được tới server. Kiểm tra IP và cổng.");
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
            ShowError("Đã xảy ra lỗi không xác định. Vui lòng thử lại sau.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void chkHienMatKhau_CheckedChanged(object? sender, EventArgs e)
    {
        txtMatKhau.UseSystemPasswordChar = !chkHienMatKhau.Checked;
        txtXacNhanMatKhau.UseSystemPasswordChar = !chkHienMatKhau.Checked;
    }

    private void btnHuy_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void ClearErrors()
    {
        lblLoiTenDangNhap.Text = "";
        lblLoiMatKhau.Text = "";
        lblLoiXacNhanMatKhau.Text = "";
        lblLoiEmail.Text = "";
        lblLoiHoTen.Text = "";
        lblLoiNgaySinh.Text = "";
        lblThongBao.Text = "";
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        btnDangKy.Enabled = !busy;
        btnDangKy.Text = busy ? "Đang xử lý..." : "Đăng ký";
        btnHuy.Enabled = !busy;
        txtTenDangNhap.Enabled = !busy;
        txtMatKhau.Enabled = !busy;
        txtXacNhanMatKhau.Enabled = !busy;
        txtEmail.Enabled = !busy;
        txtHoTen.Enabled = !busy;
        dtpNgaySinh.Enabled = !busy;

        if (busy && message is not null)
            lblThongBao.Text = message;
    }

    private void ShowError(string message)
    {
        lblThongBao.ForeColor = FormStyle.Error;
        lblThongBao.Text = message;
    }
}
