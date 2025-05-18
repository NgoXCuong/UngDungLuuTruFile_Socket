using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
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

        private void ConnectToServer()
        {
            try
            {
                client = new TcpClient("localhost", 5000);
                stream = client.GetStream();
            }
            catch (Exception ex)
            {
                throw new Exception($"Không thể kết nối đến server: {ex.Message}");
            }
        }

        private void SendRequest(string request)
        {
            try
            {
                if (client == null || !client.Connected)
                    ConnectToServer();

                byte[] data = Encoding.UTF8.GetBytes(request);
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi gửi yêu cầu: {ex.Message}");
            }
        }

        private string ReceiveResponse()
        {
            try
            {
                byte[] buffer = new byte[4096];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    throw new IOException("Server đã đóng kết nối.");

                return Encoding.UTF8.GetString(buffer, 0, bytesRead);
            }
            catch (IOException ex)
            {
                throw new Exception($"Lỗi kết nối: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi nhận phản hồi: {ex.Message}");
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi ngắt kết nối: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            try
            {
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

                ConnectToServer();
                string username = txtUserName.Text;
                string password = txtPassword.Text;

                SendRequest($"REGISTER|{username}|{password}");
                string response = ReceiveResponse();

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
    }
}
