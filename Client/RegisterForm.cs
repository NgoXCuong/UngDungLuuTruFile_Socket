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
                // Disable UI during registration
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

                // Connect to server
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

                // Send REGISTER request
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
                Console.WriteLine($"[SendRequest] Sent: {request}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendRequest] Error: {ex.Message}");
                throw;
            }
        }

        private async Task<string> ReceiveResponseAsync()
        {
            try
            {
                byte[] buffer = new byte[8192];
                StringBuilder responseBuilder = new StringBuilder();
                int bytesRead;

                // Read until newline is found
                do
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        throw new IOException("Server đã đóng kết nối.");

                    string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    responseBuilder.Append(chunk);

                    if (chunk.Contains("\n"))
                        break;
                } while (bytesRead > 0);

                string response = responseBuilder.ToString().Trim();
                Console.WriteLine($"[ReceiveResponse] Received: {response}");
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReceiveResponse] Error: {ex.Message}");
                throw;
            }
        }

        private void Disconnect()
        {
            try
            {
                stream?.Close();
                client?.Close();
                client = null;
                stream = null;
                Console.WriteLine("[Disconnect] Connection closed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Disconnect] Error: {ex.Message}");
            }
        }
    }
}