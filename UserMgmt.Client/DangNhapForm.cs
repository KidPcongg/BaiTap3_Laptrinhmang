using System.Net.Sockets;
using UserMgmt.Shared;

namespace UserMgmt.Client;

public partial class DangNhapForm : Form
{
    private readonly ServerConnection _connection = new();
    private bool _busy;
    private bool _closing;

    public DangNhapForm()
    {
        InitializeComponent();
        FormStyle.Apply(this);

        _connection.StatusChanged += msg =>
        {
            if (!IsDisposed && IsHandleCreated)
            {
                BeginInvoke(() =>
                {
                    lblTrangThai.Text = msg;
                    lblTrangThai.ForeColor = _connection.IsConnected ? FormStyle.Teal : Color.DimGray;
                });
            }
        };
    }

    private async Task EnsureConnectedAsync()
    {
        if (_connection.IsConnected)
            return;

        string host = txtDiaChiIP.Text.Trim();
        if (string.IsNullOrWhiteSpace(host))
            host = "127.0.0.1";

        int port = (int)numCong.Value;
        lblTrangThai.Text = $"Đang kết nối tới {host}:{port}...";
        lblTrangThai.ForeColor = FormStyle.Teal;

        await _connection.ConnectAsync(host, port);
    }

    private async void btnKetNoi_Click(object? sender, EventArgs e)
    {
        if (_busy) return;
        SetBusy(true, "Đang kết nối...");
        lblThongBao.Text = "";

        try
        {
            await EnsureConnectedAsync();
        }
        catch (SocketException)
        {
            ShowConnectionError("Không kết nối được tới server. Kiểm tra IP, cổng và đảm bảo server đang chạy.");
        }
        catch (TimeoutException)
        {
            ShowConnectionError("Hết thời gian chờ kết nối tới server.");
        }
        catch (Exception ex)
        {
            ShowConnectionError($"Lỗi kết nối: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void btnDangNhap_Click(object? sender, EventArgs e)
    {
        if (_busy) return;
        ClearErrors();

        string username = txtTenDangNhap.Text.Trim();
        string password = txtMatKhau.Text;

        bool hasError = false;
        if (string.IsNullOrWhiteSpace(username))
        {
            lblLoiTenDangNhap.Text = "Vui lòng nhập tên đăng nhập.";
            hasError = true;
        }

        if (string.IsNullOrEmpty(password))
        {
            lblLoiMatKhau.Text = "Vui lòng nhập mật khẩu.";
            hasError = true;
        }

        if (hasError)
        {
            if (string.IsNullOrWhiteSpace(username))
                txtTenDangNhap.Focus();
            else
                txtMatKhau.Focus();
            return;
        }

        SetBusy(true, "Đang đăng nhập...");
        try
        {
            await EnsureConnectedAsync();

            var request = new Request(Commands.DangNhap, new()
            {
                [Fields.Username] = username,
                [Fields.Password] = password
            });

            Response resp = await _connection.SendAsync(request);

            if (_closing) return;

            if (resp.Ok && resp.Data is not null)
            {
                txtMatKhau.Clear();
                SetBusy(false);
                Hide();

                using var formChinh = new TrangChinhForm(_connection, resp.Data);
                formChinh.ShowDialog(this);

                if (!_closing && !IsDisposed)
                {
                    Show();
                    Activate();
                    lblThongBao.ForeColor = formChinh.DialogResult == DialogResult.OK ? FormStyle.Teal : FormStyle.Error;
                    lblThongBao.Text = formChinh.StatusMessage;
                    txtMatKhau.Clear();
                    txtMatKhau.Focus();
                }
            }
            else
            {
                lblThongBao.ForeColor = FormStyle.Error;
                lblThongBao.Text = resp.Message;
                txtMatKhau.Clear();
                txtMatKhau.Focus();
            }
        }
        catch (SocketException)
        {
            ShowConnectionError("Không kết nối được tới server. Kiểm tra IP, cổng và đảm bảo server đang chạy.");
        }
        catch (IOException)
        {
            ShowConnectionError("Mất kết nối tới server. Vui lòng thử lại.");
        }
        catch (TimeoutException)
        {
            ShowConnectionError("Server không phản hồi kịp thời.");
        }
        catch (Exception ex)
        {
            ShowConnectionError($"Lỗi: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void lnkDangKy_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (_busy) return;
        ClearErrors();

        using var formDangKy = new DangKyForm(_connection, txtDiaChiIP.Text.Trim(), (int)numCong.Value);
        if (formDangKy.ShowDialog(this) == DialogResult.OK)
        {
            if (!string.IsNullOrEmpty(formDangKy.CreatedUsername))
            {
                txtTenDangNhap.Text = formDangKy.CreatedUsername;
                txtMatKhau.Clear();
                txtMatKhau.Focus();
                lblThongBao.ForeColor = FormStyle.Teal;
                lblThongBao.Text = "Đăng ký thành công! Hãy nhập mật khẩu để đăng nhập.";
            }
        }
    }

    private void chkHienMatKhau_CheckedChanged(object? sender, EventArgs e)
    {
        txtMatKhau.UseSystemPasswordChar = !chkHienMatKhau.Checked;
    }

    private void btnThoat_Click(object? sender, EventArgs e)
    {
        Close();
    }

    private void DangNhapForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _closing = true;
        _connection.Dispose();
    }

    private void ClearErrors()
    {
        lblLoiTenDangNhap.Text = "";
        lblLoiMatKhau.Text = "";
        lblThongBao.Text = "";
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        btnDangNhap.Enabled = !busy;
        btnKetNoi.Enabled = !busy;
        lnkDangKy.Enabled = !busy;
        txtTenDangNhap.Enabled = !busy;
        txtMatKhau.Enabled = !busy;
        txtDiaChiIP.Enabled = !busy;
        numCong.Enabled = !busy;

        if (busy && message is not null)
            lblThongBao.Text = message;
    }

    private void ShowConnectionError(string message)
    {
        lblThongBao.ForeColor = FormStyle.Error;
        lblThongBao.Text = message;
        lblTrangThai.Text = "Chưa kết nối";
        lblTrangThai.ForeColor = FormStyle.Error;
    }
}
