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
    public partial class LoginForm : Form
    {
        private TcpClient client;
        private NetworkStream stream;

        public LoginForm()
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

                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Nhận phản hồi: {response}"); // Log để debug
                return response;
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

        private void btnLogin_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtUserName.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
                {
                    MessageBox.Show("Vui lòng nhập cả tài khoản và mật khẩu.", "Lỗi đầu vào", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ConnectToServer();
                string username = txtUserName.Text;
                if (string.IsNullOrEmpty(username))
                {
                    MessageBox.Show("Tên người dùng không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                string password = txtPassword.Text;

                SendRequest($"LOGIN|{username}|{password}");
                string response = ReceiveResponse();

                if (response.StartsWith("SUCCESS"))
                {
                    using (var mainForm = new MainForm())
                    {
                        mainForm.CurrentUser = username; // Đảm bảo username không rỗng
                        Console.WriteLine($"Gán CurrentUser: {username}"); // Thêm log để debug
                        this.Hide();
                        mainForm.ShowDialog();
                        this.Close();
                    }
                }
                else
                {
                    MessageBox.Show(response, "Đăng nhập thất bại", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đăng nhập: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Không gọi Disconnect ở đây
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void lbRegister_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            using (var registerForm = new RegisterForm())
            {
                registerForm.ShowDialog();
            }
        }

        private void LoginForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect();
        }
    }
}
