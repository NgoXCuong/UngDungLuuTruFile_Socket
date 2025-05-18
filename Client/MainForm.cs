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
    public partial class MainForm : Form
    {
        private TcpClient client;
        private NetworkStream stream;

        public string CurrentUser { get; set; }

        public MainForm()
        {
            InitializeComponent();

            ConnectToServer();
        }

        private void RefreshFileList()
        {
            try
            {
                if (string.IsNullOrEmpty(CurrentUser))
                {
                    MessageBox.Show("Người dùng chưa được xác định.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                listFoder.Items.Clear();
                ConnectToServer();
                SendRequest($"LIST|{CurrentUser}");
                string response = ReceiveResponse();
                Console.WriteLine($"Phản hồi LIST: {response}");

                if (response.StartsWith("SUCCESS"))
                {
                    string[] items = response.Split('|').Skip(1).ToArray();
                    listFoder.Items.AddRange(items);
                }
                else
                {
                    MessageBox.Show(response, "Lỗi danh sách tệp", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi làm mới danh sách tệp: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Disconnect();
            }
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

        public void SetConnection(TcpClient tcpClient, NetworkStream networkStream)
        {
            client = tcpClient;
            stream = networkStream;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentUser))
            {
                MessageBox.Show("Không thể tải danh sách tệp: Người dùng chưa đăng nhập.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }
            RefreshFileList();
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            try
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        string filename = Path.GetFileName(ofd.FileName);
                        byte[] fileData = File.ReadAllBytes(ofd.FileName);
                        string base64Data = Convert.ToBase64String(fileData);

                        ConnectToServer();
                        SendRequest($"UPLOAD|{CurrentUser}|{filename}|{base64Data}");
                        string response = ReceiveResponse();
                        MessageBox.Show(response, "Kết quả tải lên", MessageBoxButtons.OK, response.StartsWith("SUCCESS") ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                        RefreshFileList();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải lên: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Disconnect();
            }
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            if (listFoder.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một tệp để tải xuống.", "Lỗi lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string filename = listFoder.SelectedItem.ToString();
                ConnectToServer();
                SendRequest($"DOWNLOAD|{CurrentUser}|{filename}");
                string response = ReceiveResponse();

                if (response.StartsWith("SUCCESS"))
                {
                    string base64Data = response.Split('|')[1];
                    byte[] fileData = Convert.FromBase64String(base64Data);

                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.FileName = filename;
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            File.WriteAllBytes(sfd.FileName, fileData);
                            MessageBox.Show("Tệp đã được tải xuống thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
                else
                {
                    MessageBox.Show(response, "Tải xuống thất bại", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải xuống: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Disconnect();
            }
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            try
            {
                string dirName = Prompt.ShowDialog("Nhập tên thư mục:", "Tạo thư mục");
                if (!string.IsNullOrEmpty(dirName))
                {
                    ConnectToServer();
                    SendRequest($"CREATE_DIR|{CurrentUser}|{dirName}");
                    string response = ReceiveResponse();
                    MessageBox.Show(response, "Kết quả tạo thư mục", MessageBoxButtons.OK, response.StartsWith("SUCCESS") ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                    RefreshFileList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tạo thư mục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Disconnect();
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (listFoder.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một mục để xóa.", "Lỗi lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string itemName = listFoder.SelectedItem.ToString();
                ConnectToServer();
                SendRequest($"DELETE|{CurrentUser}|{itemName}");
                string response = ReceiveResponse();
                MessageBox.Show(response, "Kết quả xóa", MessageBoxButtons.OK, response.StartsWith("SUCCESS") ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                RefreshFileList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xóa: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Disconnect();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect();
        }
    }
}

