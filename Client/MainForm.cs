using System;
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
        }

        class FileItem
        {
            public string Name { get; set; }
            public bool IsDirectory { get; set; }

            public override string ToString()
            {
                return IsDirectory ? $"[Dir]{Name}" : $"[File]{Name}";
            }
        }

        private async Task RefreshFileListAsync()
        {
            try
            {
                btnDelete.Enabled = false;
                btnDownload.Enabled = false;
                listFoder.Items.Clear();

                await SendRequestAsync($"LIST|{CurrentUser}");
                string response = await ReceiveResponseAsync();

                if (response.StartsWith("SUCCESS"))
                {
                    var parts = response.Split('|').Skip(1).Where(x => !string.IsNullOrEmpty(x));
                    foreach (var item in parts)
                    {
                        bool isDir = item.StartsWith("[Dir]");
                        string name = isDir ? item.Substring(5) : item.Substring(6);
                        listFoder.Items.Add(new FileItem { Name = name, IsDirectory = isDir });
                    }
                }
                else
                {
                    MessageBox.Show(response, "Lỗi danh sách tệp", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi làm mới danh sách tệp: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RedirectToLogin();
            }
        }

        public void SetConnection(TcpClient tcpClient, NetworkStream networkStream)
        {
            client = tcpClient;
            stream = networkStream;
        }

        private async Task SendRequestAsync(string request)
        {
            try
            {
                if (client == null || !client.Connected || stream == null)
                {
                    throw new Exception("Không có kết nối đến server. Vui lòng đăng nhập lại.");
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

        private void RedirectToLogin()
        {
            MessageBox.Show("Mất kết nối với server. Vui lòng đăng nhập lại.", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Disconnect();
            using (var loginForm = new LoginForm())
            {
                this.Hide();
                loginForm.ShowDialog();
                this.Close();
            }
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentUser))
            {
                MessageBox.Show("Không thể tải danh sách tệp: Người dùng chưa đăng nhập.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RedirectToLogin();
                return;
            }
            btnDelete.Enabled = false;
            btnDownload.Enabled = false;
            await RefreshFileListAsync();
        }

        private async void btnUpload_Click(object sender, EventArgs e)
        {
            try
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        string filename = Path.GetFileName(ofd.FileName);
                        if (string.IsNullOrEmpty(filename) || Path.GetInvalidFileNameChars().Any(filename.Contains))
                        {
                            MessageBox.Show("Tên tệp chứa ký tự không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        byte[] fileData;
                        try
                        {
                            fileData = File.ReadAllBytes(ofd.FileName);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Không thể đọc tệp: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        if (fileData.Length == 0)
                        {
                            MessageBox.Show("Tệp rỗng, không thể tải lên.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        if (fileData.Length > 100 * 1024 * 1024)
                        {
                            MessageBox.Show("Tệp quá lớn (giới hạn 100MB).", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        string base64Data = Convert.ToBase64String(fileData);
                        Console.WriteLine($"[Upload] File: {filename}, Size: {fileData.Length} bytes, Base64 length: {base64Data.Length}");
                        string request = $"UPLOAD|{CurrentUser}|{filename}|{base64Data}";
                        await SendRequestAsync(request);
                        string response = await ReceiveResponseAsync();

                        MessageBox.Show(response, "Kết quả tải lên", MessageBoxButtons.OK, response.StartsWith("SUCCESS") ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                        await RefreshFileListAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải lên: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RedirectToLogin();
            }
        }

        private async void btnDownload_Click(object sender, EventArgs e)
        {
            if (listFoder.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một tệp để tải xuống.", "Lỗi lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FileItem selectedItem = listFoder.SelectedItem as FileItem;
            if (selectedItem == null || selectedItem.IsDirectory)
            {
                MessageBox.Show("Chỉ có thể tải xuống tệp, không phải thư mục.", "Lỗi lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string filename = selectedItem.Name;
                await SendRequestAsync($"DOWNLOAD|{CurrentUser}|{filename}");
                string response = await ReceiveResponseAsync();

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
                RedirectToLogin();
            }
        }

        private async void btnCreate_Click(object sender, EventArgs e)
        {
            try
            {
                string dirName = Prompt.ShowDialog("Nhập tên thư mục:", "Tạo thư mục");
                if (!string.IsNullOrEmpty(dirName))
                {
                    if (Path.GetInvalidFileNameChars().Any(dirName.Contains))
                    {
                        MessageBox.Show("Tên thư mục chứa ký tự không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    await SendRequestAsync($"CREATE_DIR|{CurrentUser}|{dirName}");
                    string response = await ReceiveResponseAsync();
                    MessageBox.Show(response, "Kết quả tạo thư mục", MessageBoxButtons.OK, response.StartsWith("SUCCESS") ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                    await RefreshFileListAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tạo thư mục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RedirectToLogin();
            }
        }

        private async void btnDelete_Click(object sender, EventArgs e)
        {
            if (listFoder.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một mục để xóa.", "Lỗi lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FileItem selectedItem = listFoder.SelectedItem as FileItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Dữ liệu chọn không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                await SendRequestAsync($"DELETE|{CurrentUser}|{selectedItem.Name}");
                string response = await ReceiveResponseAsync();
                MessageBox.Show(response, "Kết quả xóa", MessageBoxButtons.OK, response.StartsWith("SUCCESS") ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                await RefreshFileListAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xóa: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RedirectToLogin();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect();
        }

        private void listFoder_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool hasSelection = listFoder.SelectedItem != null;
            btnDelete.Enabled = hasSelection;
            btnDownload.Enabled = hasSelection && (listFoder.SelectedItem as FileItem)?.IsDirectory == false;
        }
    }
}