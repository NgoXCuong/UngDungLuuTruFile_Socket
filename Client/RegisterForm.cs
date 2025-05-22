using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public partial class RegisterForm : Form
    {
        private TcpClient client;
        private NetworkStream stream;

        public RegisterForm()
        {
            InitializeComponent();
        }

        private async void btnRegister_Click(object sender, EventArgs e)
        {
            try
            {
                // vô hiệu hóa các điều khiển và hiển thị con trỏ chờ trong khi đang xử lý
                btnRegister.Enabled = false;
                btnExit.Enabled = false;
                lbRegister.Enabled = false;
                txtUserName.Enabled = false;
                txtPassword.Enabled = false;
                txtConfirmPass.Enabled = false;
                Cursor = Cursors.WaitCursor;

                if (string.IsNullOrWhiteSpace(txtUserName.Text) || string.IsNullOrWhiteSpace(txtPassword.Text) || string.IsNullOrWhiteSpace(txtConfirmPass.Text))
                {
                    MessageBox.Show("Vui lòng nhập đầy đủ tài khoản, mật khẩu và xác nhận mật khẩu.", "Lỗi đầu vào", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (txtPassword.Text != txtConfirmPass.Text)
                {
                    MessageBox.Show("Mật khẩu và xác nhận mật khẩu không khớp!", "Lỗi đầu vào", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Kết nối đến server
                client = new TcpClient();
                try
                {
                    await client.ConnectAsync("localhost", 5000);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Không thể kết nối đến server: {ex.Message}");
                }
                stream = client.GetStream();

                string username = txtUserName.Text.Trim();
                string password = txtPassword.Text.Trim();

                // Gửi phản hồi đăng ký đến server
                await SendRequestAsync($"REGISTER|{username}|{password}");
                string response = await ReceiveResponseAsync();

                MessageBox.Show(response.StartsWith("SUCCESS") ? "Đăng ký thành công!" : response, "Kết quả đăng ký", MessageBoxButtons.OK, response.StartsWith("SUCCESS") ? MessageBoxIcon.Information : MessageBoxIcon.Error);

                if (response.StartsWith("SUCCESS"))
                {
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đăng ký: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Disconnect();
                // Re-enable UI
                btnRegister.Enabled = true;
                btnExit.Enabled = true;
                lbRegister.Enabled = true;
                txtUserName.Enabled = true;
                txtPassword.Enabled = true;
                txtConfirmPass.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void lbRegister_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            using (var loginForm = new LoginForm())
            {
                this.Hide();
                loginForm.ShowDialog();
                this.Close();
            }
        }

        private void RegisterForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect();
        }

        // Khai báo 1 phương thức bất đồng bộ để gửi yêu cầu đến server
        private async Task SendRequestAsync(string request)
        {
            try
            {
                if (client == null || !client.Connected || stream == null)
                {
                    throw new Exception("Không có kết nối đến server.");
                }

                request = request.Trim();
                byte[] data = Encoding.UTF8.GetBytes(request + "\n");
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                Console.WriteLine($"[SendRequest] Gửi: {request}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendRequest] Lỗi: {ex.Message}");
                throw;
            }
        }

        // Khai báo 1 phương thức bất đồng bộ để nhận phản hồi từ server
        private async Task<string> ReceiveResponseAsync()
        {
            try
            {
                byte[] buffer = new byte[8192]; // lưu dữ liệu nhận được từ server (8KB).

                // ghép nối các phần dữ liệu đọc được thành chuỗi hoàn chỉnh.
                StringBuilder responseBuilder = new StringBuilder(); 
                int bytesRead; // số byte đọc được trong mỗi lần ReadAsync

                do
                {
                    // đọc dữ liệu một cách bất đồng bộ từ stream
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        throw new IOException("Server đã đóng kết nối.");

                    string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    responseBuilder.Append(chunk); // để ghép nối các phần dữ liệu.

                    if (chunk.Contains("\n"))
                        break;
                } while (bytesRead > 0);

                string response = responseBuilder.ToString().Trim();
                Console.WriteLine($"[ReceiveResponse] Đã nhận: {response}");
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReceiveResponse] Lỗi: {ex.Message}");
                throw;
            }
        }

        private void Disconnect()
        {
            try
            {
                // Gọi phương thức Close() để đóng stream và client nếu đồi tượn khác null
                stream?.Close(); 
                client?.Close();
                client = null; // Giải phóng tài nguyên
                stream = null;
                Console.WriteLine("[Disconnect] Đóng kết nối.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Disconnect] Lỗi: {ex.Message}");
            }
        }
    }
}